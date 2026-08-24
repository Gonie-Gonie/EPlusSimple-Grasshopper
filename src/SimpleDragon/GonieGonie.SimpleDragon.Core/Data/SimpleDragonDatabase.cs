using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

/// <summary>
/// Immutable aggregate of all packaged SimpleDragon lookup databases.
/// </summary>
public sealed class SimpleDragonDatabase
{
    private static readonly Lazy<SimpleDragonDatabase> Shared = new(
        LoadEmbedded,
        LazyThreadSafetyMode.ExecutionAndPublication);

    private SimpleDragonDatabase(
        MaterialDatabase materials,
        SurfaceConstructionDatabase surfaceConstructions,
        FenestrationConstructionDatabase fenestrationConstructions,
        UsageProfileDatabase usageProfiles,
        KoreanHolidayDatabase holidays,
        WeatherDatabase weather)
    {
        Materials = materials;
        SurfaceConstructions = surfaceConstructions;
        FenestrationConstructions = fenestrationConstructions;
        UsageProfiles = usageProfiles;
        Holidays = holidays;
        Weather = weather;
    }

    public static SimpleDragonDatabase Default => Shared.Value;

    public MaterialDatabase Materials { get; }

    public SurfaceConstructionDatabase SurfaceConstructions { get; }

    public FenestrationConstructionDatabase FenestrationConstructions { get; }

    public UsageProfileDatabase UsageProfiles { get; }

    public KoreanHolidayDatabase Holidays { get; }

    public WeatherDatabase Weather { get; }

    public static SimpleDragonDatabase LoadEmbedded()
    {
        MaterialDatabase materials = new(
            CsvDocument.ReadEmbedded(SimpleDragonEmbeddedData.Material));
        SurfaceConstructionDatabase surfaces = new(
            CsvDocument.ReadEmbedded(SimpleDragonEmbeddedData.SurfaceRegulations),
            materials);
        FenestrationConstructionDatabase fenestrations = new(
            CsvDocument.ReadEmbedded(SimpleDragonEmbeddedData.FenestrationRegulations));
        UsageProfileDatabase profiles = new(
            CsvDocument.ReadEmbedded(SimpleDragonEmbeddedData.KoreanUsageProfile, stripHeaderUnits: true),
            CsvDocument.ReadEmbedded(SimpleDragonEmbeddedData.KoreanUsageProfileExtended, stripHeaderUnits: true));
        KoreanHolidayDatabase holidays = new(
            CsvDocument.ReadEmbedded(SimpleDragonEmbeddedData.KoreanHoliday));
        WeatherDatabase weather = new(
            CsvDocument.ReadEmbedded(SimpleDragonEmbeddedData.AddressWeather),
            CsvDocument.ReadEmbedded(SimpleDragonEmbeddedData.ClimateRegions));

        return new SimpleDragonDatabase(materials, surfaces, fenestrations, profiles, holidays, weather);
    }
}
