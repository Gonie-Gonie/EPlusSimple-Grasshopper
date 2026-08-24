using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

/// <summary>
/// Weather-station metadata associated with a Korean administrative district.
/// </summary>
public sealed class WeatherMetadata
{
    internal WeatherMetadata(
        string administrativeArea,
        string legalDistrictCode,
        string terrain,
        double administrativeLatitude,
        double administrativeLongitude,
        string weatherLocation,
        string weatherLocationType,
        double weatherLatitude,
        double weatherLongitude,
        string epwFileName)
    {
        AdministrativeArea = DomainSupport.RequiredText(administrativeArea, nameof(administrativeArea));
        LegalDistrictCode = DomainSupport.RequiredText(legalDistrictCode, nameof(legalDistrictCode));
        Terrain = DomainSupport.RequiredText(terrain, nameof(terrain));
        AdministrativeLatitude = administrativeLatitude;
        AdministrativeLongitude = administrativeLongitude;
        WeatherLocation = DomainSupport.RequiredText(weatherLocation, nameof(weatherLocation));
        WeatherLocationType = DomainSupport.RequiredText(weatherLocationType, nameof(weatherLocationType));
        WeatherLatitude = weatherLatitude;
        WeatherLongitude = weatherLongitude;
        EpwFileName = DomainSupport.RequiredText(epwFileName, nameof(epwFileName));
        Id = DeterministicDomainId.Create("WTHR-DB", AdministrativeArea);
    }

    public EntityId Id { get; }

    public string AdministrativeArea { get; }

    public string LegalDistrictCode { get; }

    public string Terrain { get; }

    public double AdministrativeLatitude { get; }

    public double AdministrativeLongitude { get; }

    public string WeatherLocation { get; }

    public string WeatherLocationType { get; }

    public double WeatherLatitude { get; }

    public double WeatherLongitude { get; }

    public string EpwFileName { get; }
}

/// <summary>
/// Portable result of resolving a Korean address and construction vintage.
/// </summary>
public sealed class WeatherSelection
{
    internal WeatherSelection(WeatherMetadata metadata, string climateRegion, DateTime climateEffectiveDate)
    {
        Metadata = metadata;
        ClimateRegion = climateRegion;
        ClimateEffectiveDate = climateEffectiveDate.Date;
    }

    public WeatherMetadata Metadata { get; }

    public string Terrain => Metadata.Terrain;

    public string ClimateRegion { get; }

    public DateTime ClimateEffectiveDate { get; }

    public string WeatherLocation => Metadata.WeatherLocation;

    public string EpwFileName => Metadata.EpwFileName;

    public string ResolveEpwPath(string weatherDirectory)
    {
        string directory = DomainSupport.RequiredText(weatherDirectory, nameof(weatherDirectory));
        return Path.Combine(directory, EpwFileName);
    }
}

/// <summary>
/// Address-to-weather and date-sensitive Korean climate-region metadata.
/// </summary>
public sealed class WeatherDatabase
{
    private readonly ReadOnlyDictionary<string, WeatherMetadata> _byAdministrativeArea;
    private readonly ReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, string>> _climates;
    private readonly IReadOnlyList<string> _addressesByDescendingLength;

    internal WeatherDatabase(CsvDocument weatherDocument, CsvDocument climateDocument)
    {
        var items = new List<WeatherMetadata>(weatherDocument.Rows.Count);
        var weather = new Dictionary<string, WeatherMetadata>(StringComparer.Ordinal);
        foreach (CsvRow row in weatherDocument.Rows)
        {
            var item = new WeatherMetadata(
                row.Required("행정구역명"),
                row.Required("법정동코드"),
                row.Required("terrain"),
                row.Number("행정구역위도"),
                row.Number("행정구역경도"),
                row.Required("기상지역명"),
                row.Required("기상지역유형"),
                row.Number("기상지역위도"),
                row.Number("기상지역경도"),
                row.Required("EPW파일명"));
            if (weather.ContainsKey(item.AdministrativeArea))
            {
                throw row.Error("Duplicate administrative area '" + item.AdministrativeArea + "'.");
            }

            weather.Add(item.AdministrativeArea, item);
            items.Add(item);
        }

        var climates = new Dictionary<string, IReadOnlyDictionary<DateTime, string>>(StringComparer.Ordinal);
        string[] dateColumns = climateDocument.Headers.Skip(1).ToArray();
        foreach (CsvRow row in climateDocument.Rows)
        {
            string area = row.Required("행정구역명");
            var byDate = new SortedDictionary<DateTime, string>();
            foreach (string dateColumn in dateColumns)
            {
                if (!DateTime.TryParseExact(
                        dateColumn,
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime date))
                {
                    throw row.Error("Climate column '" + dateColumn + "' must use yyyyMMdd format.");
                }

                byDate.Add(date.Date, row.Required(dateColumn));
            }

            if (climates.ContainsKey(area))
            {
                throw row.Error("Duplicate climate area '" + area + "'.");
            }

            climates.Add(area, new ReadOnlyDictionary<DateTime, string>(byDate));
        }

        Items = items.AsReadOnly();
        _byAdministrativeArea = new ReadOnlyDictionary<string, WeatherMetadata>(weather);
        _climates = new ReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, string>>(climates);
        _addressesByDescendingLength = items
            .Select(item => item.AdministrativeArea)
            .OrderByDescending(value => value.Length)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<WeatherMetadata> Items { get; }

    public LookupResult<WeatherSelection> FindByAddress(string? address, DateTime vintage)
    {
        string normalizedAddress = NormalizeAddress(address);
        if (normalizedAddress.Length == 0)
        {
            return LookupResults.Failure<WeatherSelection>(new Diagnostic(
                "SD.WEATHER.ADDRESS_REQUIRED",
                DiagnosticSeverity.Error,
                "A Korean address is required.",
                suggestedAction: "Provide an address beginning with a supported 시, 군, or 구 district."));
        }

        string? administrativeArea = null;
        foreach (string candidate in _addressesByDescendingLength)
        {
            if (StringComparer.Ordinal.Equals(normalizedAddress, candidate)
                || normalizedAddress.StartsWith(candidate + " ", StringComparison.Ordinal))
            {
                administrativeArea = candidate;
                break;
            }
        }

        if (administrativeArea is null
            || !_byAdministrativeArea.TryGetValue(administrativeArea, out WeatherMetadata? metadata))
        {
            return LookupResults.Failure<WeatherSelection>(new Diagnostic(
                "SD.WEATHER.ADDRESS_NOT_FOUND",
                DiagnosticSeverity.Error,
                "No packaged weather metadata matches address '" + normalizedAddress + "'.",
                suggestedAction: "Begin the address with a Korean administrative district listed in WeatherDatabase.Items."));
        }

        if (!_climates.TryGetValue(administrativeArea, out IReadOnlyDictionary<DateTime, string>? climateByDate))
        {
            return LookupResults.Failure<WeatherSelection>(new Diagnostic(
                "SD.WEATHER.CLIMATE_ROW_MISSING",
                DiagnosticSeverity.Error,
                "Climate metadata is missing for '" + administrativeArea + "'.",
                suggestedAction: "Verify the packaged climate-region data."));
        }

        DateTime effectiveDate = DateTime.MinValue;
        string? climateRegion = null;
        foreach (KeyValuePair<DateTime, string> entry in climateByDate.OrderBy(item => item.Key))
        {
            if (entry.Key <= vintage.Date)
            {
                effectiveDate = entry.Key;
                climateRegion = entry.Value;
            }
            else
            {
                break;
            }
        }

        if (climateRegion is null)
        {
            return LookupResults.Failure<WeatherSelection>(new Diagnostic(
                "SD.WEATHER.VINTAGE_NOT_COVERED",
                DiagnosticSeverity.Error,
                "No climate region is available for '" + administrativeArea + "' on "
                + vintage.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".",
                suggestedAction: "Use a vintage on or after the earliest packaged climate regulation."));
        }

        return LookupResults.Success(
            new WeatherSelection(metadata, climateRegion, effectiveDate));
    }

    private static string NormalizeAddress(string? address)
    {
        return address is null
            ? string.Empty
            : Regex.Replace(address.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
    }
}
