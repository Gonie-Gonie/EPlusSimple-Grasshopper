using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon;

public enum UsageDay
{
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday,
    Holiday,
}

public enum UsageProfileSource
{
    Standard,
    Extended,
    Custom,
}

/// <summary>
/// A month/day pair independent of a particular calendar year.
/// </summary>
public sealed class MonthDay : IEquatable<MonthDay>
{
    public MonthDay(int month, int day)
    {
        if (month < 1 || month > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be between 1 and 12.");
        }

        int maximumDay = DateTime.DaysInMonth(2000, month);
        if (day < 1 || day > maximumDay)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "Day is invalid for the month.");
        }

        Month = month;
        Day = day;
    }

    public int Month { get; }

    public int Day { get; }

    public bool Equals(MonthDay? other)
    {
        return other is not null && Month == other.Month && Day == other.Day;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as MonthDay);
    }

    public override int GetHashCode()
    {
        return (Month * 397) ^ Day;
    }

    public override string ToString()
    {
        return Month.ToString("00", System.Globalization.CultureInfo.InvariantCulture)
            + "/"
            + Day.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed class VacationPeriod
{
    public VacationPeriod(MonthDay start, MonthDay end)
    {
        Start = DomainSupport.NotNull(start, nameof(start));
        End = DomainSupport.NotNull(end, nameof(end));
    }

    public MonthDay Start { get; }

    public MonthDay End { get; }
}

/// <summary>
/// A Korean building-usage profile from the embedded standard or extension table.
/// </summary>
public sealed class UsageProfile
{
    private static readonly UsageDay[] OrderedDays =
    {
        UsageDay.Monday,
        UsageDay.Tuesday,
        UsageDay.Wednesday,
        UsageDay.Thursday,
        UsageDay.Friday,
        UsageDay.Saturday,
        UsageDay.Sunday,
        UsageDay.Holiday,
    };

    private readonly IReadOnlyDictionary<UsageDay, bool> _operation;

    public UsageProfile(
        string name,
        int occupantStart,
        int occupantEnd,
        int hvacStart,
        int hvacEnd,
        double ventilation,
        double domesticHotWater,
        double lightingHours,
        double occupancy,
        double equipment,
        double heatingSetpoint,
        double coolingSetpoint,
        IReadOnlyDictionary<UsageDay, bool> operation,
        IEnumerable<VacationPeriod>? vacations = null,
        UsageProfileSource source = UsageProfileSource.Standard,
        EntityId? id = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        ValidateHour(occupantStart, nameof(occupantStart));
        ValidateHour(occupantEnd, nameof(occupantEnd));
        ValidateHour(hvacStart, nameof(hvacStart));
        ValidateHour(hvacEnd, nameof(hvacEnd));
        OccupantStart = occupantStart;
        OccupantEnd = occupantEnd;
        HvacStart = hvacStart;
        HvacEnd = hvacEnd;
        Ventilation = DomainSupport.FiniteNonNegative(ventilation, nameof(ventilation));
        DomesticHotWater = DomainSupport.FiniteNonNegative(domesticHotWater, nameof(domesticHotWater));
        LightingHours = DomainSupport.FiniteNonNegative(lightingHours, nameof(lightingHours));
        Occupancy = DomainSupport.FiniteNonNegative(occupancy, nameof(occupancy));
        Equipment = DomainSupport.FiniteNonNegative(equipment, nameof(equipment));
        HeatingSetpoint = ValidateFinite(heatingSetpoint, nameof(heatingSetpoint));
        CoolingSetpoint = ValidateFinite(coolingSetpoint, nameof(coolingSetpoint));
        DomainSupport.NotNull(operation, nameof(operation));

        var operationCopy = new Dictionary<UsageDay, bool>();
        foreach (UsageDay day in OrderedDays)
        {
            if (!operation.TryGetValue(day, out bool operates))
            {
                throw new ArgumentException("Operation is missing " + day + ".", nameof(operation));
            }

            operationCopy.Add(day, operates);
        }

        _operation = new System.Collections.ObjectModel.ReadOnlyDictionary<UsageDay, bool>(operationCopy);
        VacationPeriod[] vacationArray = vacations?.ToArray() ?? Array.Empty<VacationPeriod>();
        if (vacationArray.Any(vacation => vacation is null))
        {
            throw new ArgumentException("A vacation period cannot be null.", nameof(vacations));
        }

        Vacations = Array.AsReadOnly(vacationArray);
        if (!Enum.IsDefined(typeof(UsageProfileSource), source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown profile source.");
        }

        Source = source;
        Id = id ?? DeterministicDomainId.Create("PRFL", Name);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public UsageProfileSource Source { get; }

    public int OccupantStart { get; }

    public int OccupantEnd { get; }

    public int HvacStart { get; }

    public int HvacEnd { get; }

    public double Ventilation { get; }

    public double DomesticHotWater { get; }

    public double LightingHours { get; }

    public double Occupancy { get; }

    public double Equipment { get; }

    public double HeatingSetpoint { get; }

    public double CoolingSetpoint { get; }

    public IReadOnlyList<VacationPeriod> Vacations { get; }

    public double OccupiedHours => OccupantEnd > OccupantStart
        ? OccupantEnd - OccupantStart
        : 24d - (OccupantStart - OccupantEnd);

    public IReadOnlyList<UsageDay> OperatingDays => Array.AsReadOnly(
        OrderedDays.Where(day => _operation[day]).ToArray());

    public bool OperatesOn(UsageDay day)
    {
        if (!Enum.IsDefined(typeof(UsageDay), day))
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "Unknown usage day.");
        }

        return _operation[day];
    }

    /// <summary>
    /// Returns the upstream-compatible profile representation in its pinned key order.
    /// </summary>
    public OrderedMap<object> ToDictionary()
    {
        IReadOnlyList<string> operatingDays = Array.AsReadOnly(
            OperatingDays
                .Select(day => day.ToString().ToLowerInvariant())
                .ToArray());
        IReadOnlyList<OrderedMap<object>> vacations = Array.AsReadOnly(
            Vacations
                .Select(period => new OrderedMap<object>(new[]
                {
                    Entry("start", period.Start.ToString()),
                    Entry("end", period.End.ToString()),
                }))
                .ToArray());

        return new OrderedMap<object>(new[]
        {
            Entry("name", Name),
            Entry("occupant_start", OccupantStart),
            Entry("occupant_end", OccupantEnd),
            Entry("hvac_start", HvacStart),
            Entry("hvac_end", HvacEnd),
            Entry("ventilation", Ventilation),
            Entry("domestic_hotwater", DomesticHotWater),
            Entry("lighting_hours", LightingHours),
            Entry("occupancy", Occupancy),
            Entry("equipment", Equipment),
            Entry("heating_setpoint", HeatingSetpoint),
            Entry("cooling_setpoint", CoolingSetpoint),
            Entry("operate_weekdays", operatingDays),
            Entry("vacations", vacations),
        });
    }

    private static KeyValuePair<string, object> Entry(string key, object value)
    {
        return new KeyValuePair<string, object>(key, value);
    }

    private static void ValidateHour(int hour, string parameterName)
    {
        if (hour < 0 || hour > 24)
        {
            throw new ArgumentOutOfRangeException(parameterName, hour, "Hour must be between 0 and 24.");
        }
    }

    private static double ValidateFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A finite value is required.");
        }

        return value;
    }
}

/// <summary>
/// A named Korean public holiday from the packaged calendar table.
/// </summary>
public sealed class KoreanHoliday
{
    public KoreanHoliday(string name, DateTime date, EntityId? id = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        Date = date.Date;
        Id = id ?? DeterministicDomainId.Create("HOLI", Name, Date);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public DateTime Date { get; }
}
