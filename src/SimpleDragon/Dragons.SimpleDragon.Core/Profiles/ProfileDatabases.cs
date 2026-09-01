using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon;

/// <summary>
/// Ordered Korean usage-profile database. Extension rows follow standard rows.
/// </summary>
public sealed class UsageProfileDatabase
{
    private static readonly Regex VacationPattern = new(
        @"(\d{1,2})/(\d{1,2})-(\d{1,2})/(\d{1,2})",
        RegexOptions.CultureInvariant);

    private readonly ReadOnlyDictionary<string, UsageProfile> _byName;

    internal UsageProfileDatabase(CsvDocument standard, CsvDocument extended)
    {
        var items = new List<UsageProfile>(standard.Rows.Count + extended.Rows.Count);
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);

        AddRows(standard.Rows, UsageProfileSource.Standard, items, positions);
        AddRows(extended.Rows, UsageProfileSource.Extended, items, positions);

        Items = items.AsReadOnly();
        _byName = new ReadOnlyDictionary<string, UsageProfile>(
            items.ToDictionary(profile => profile.Name, profile => profile, StringComparer.Ordinal));
    }

    public IReadOnlyList<UsageProfile> Items { get; }

    public LookupResult<UsageProfile> Find(string? name)
    {
        string key = name?.Trim() ?? string.Empty;
        if (key.Length > 0 && _byName.TryGetValue(key, out UsageProfile? profile))
        {
            return LookupResults.Success(profile);
        }

        return LookupResults.Failure<UsageProfile>(new Diagnostic(
            "SD.DB.PROFILE_NOT_FOUND",
            DiagnosticSeverity.Error,
            key.Length == 0
                ? "A usage-profile name is required."
                : "Usage profile '" + key + "' was not found in the embedded database.",
            suggestedAction: "Select one of UsageProfileDatabase.Items."));
    }

    private static void AddRows(
        IReadOnlyList<CsvRow> rows,
        UsageProfileSource source,
        List<UsageProfile> items,
        Dictionary<string, int> positions)
    {
        foreach (CsvRow row in rows)
        {
            string name = row.Required("Name");
            var operation = new Dictionary<UsageDay, bool>
            {
                [UsageDay.Monday] = row.ZeroOne("Monday"),
                [UsageDay.Tuesday] = row.ZeroOne("Tuesday"),
                [UsageDay.Wednesday] = row.ZeroOne("Wednesday"),
                [UsageDay.Thursday] = row.ZeroOne("Thursday"),
                [UsageDay.Friday] = row.ZeroOne("Friday"),
                [UsageDay.Saturday] = row.ZeroOne("Saturday"),
                [UsageDay.Sunday] = row.ZeroOne("Sunday"),
                [UsageDay.Holiday] = row.ZeroOne("Holiday"),
            };
            var profile = new UsageProfile(
                name,
                row.Integer("Occupant-Start"),
                row.Integer("Occupant-End"),
                row.Integer("HVAC-Start"),
                row.Integer("HVAC-End"),
                row.Number("Ventilation"),
                row.Number("DomesticHotWater"),
                row.Number("LightingHours"),
                row.Number("Occupancy"),
                row.Number("Equipment"),
                row.Number("Heating-Setpoint"),
                row.Number("Cooling-Setpoint"),
                operation,
                ParseVacations(row),
                source,
                DeterministicDomainId.Create("PRFL-DB", name));

            if (positions.TryGetValue(name, out int existingIndex))
            {
                items[existingIndex] = profile;
            }
            else
            {
                positions.Add(name, items.Count);
                items.Add(profile);
            }
        }
    }

    private static IReadOnlyList<VacationPeriod> ParseVacations(CsvRow row)
    {
        string value = row.Optional("Vacations");
        if (value.Length == 0)
        {
            return Array.Empty<VacationPeriod>();
        }

        MatchCollection matches = VacationPattern.Matches(value);
        if (matches.Count == 0)
        {
            throw row.Error("Vacations must contain MM/DD-MM/DD ranges.");
        }

        var vacations = new List<VacationPeriod>(matches.Count);
        foreach (Match match in matches)
        {
            vacations.Add(new VacationPeriod(
                new MonthDay(ParseGroup(match, 1), ParseGroup(match, 2)),
                new MonthDay(ParseGroup(match, 3), ParseGroup(match, 4))));
        }

        return vacations.AsReadOnly();
    }

    private static int ParseGroup(Match match, int group)
    {
        return int.Parse(match.Groups[group].Value, NumberStyles.None, CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Ordered packaged Korean holiday calendar.
/// </summary>
public sealed class KoreanHolidayDatabase
{
    private readonly ReadOnlyDictionary<DateTime, IReadOnlyList<KoreanHoliday>> _byDate;

    internal KoreanHolidayDatabase(CsvDocument document)
    {
        var items = new List<KoreanHoliday>(document.Rows.Count);
        var byDate = new Dictionary<DateTime, List<KoreanHoliday>>();
        foreach (CsvRow row in document.Rows)
        {
            DateTime date;
            try
            {
                date = new DateTime(row.Integer("Year"), row.Integer("Month"), row.Integer("Day"));
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw row.Error("Holiday date is invalid: " + exception.Message);
            }

            string name = row.Required("Name");
            var holiday = new KoreanHoliday(
                name,
                date,
                DeterministicDomainId.Create("HOLI-DB", name, date));
            items.Add(holiday);
            if (!byDate.TryGetValue(date, out List<KoreanHoliday>? dates))
            {
                dates = new List<KoreanHoliday>();
                byDate.Add(date, dates);
            }

            dates.Add(holiday);
        }

        Items = items.AsReadOnly();
        _byDate = new ReadOnlyDictionary<DateTime, IReadOnlyList<KoreanHoliday>>(
            byDate.ToDictionary(
                item => item.Key,
                item => (IReadOnlyList<KoreanHoliday>)item.Value.AsReadOnly()));
    }

    public IReadOnlyList<KoreanHoliday> Items { get; }

    public IReadOnlyList<KoreanHoliday> On(DateTime date)
    {
        return _byDate.TryGetValue(date.Date, out IReadOnlyList<KoreanHoliday>? holidays)
            ? holidays
            : Array.Empty<KoreanHoliday>();
    }

    public IReadOnlyList<KoreanHoliday> InYear(int year)
    {
        return Items.Where(holiday => holiday.Date.Year == year).ToArray();
    }
}
