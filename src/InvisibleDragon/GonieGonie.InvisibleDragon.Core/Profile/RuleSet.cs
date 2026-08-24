using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Profile;

/// <summary>
/// Immutable weekday/weekend defaults with optional day-specific overrides.
/// </summary>
public sealed class RuleSet : IEquatable<RuleSet>
{
    public RuleSet(
        string name,
        DaySchedule? weekdays = null,
        DaySchedule? weekends = null,
        DaySchedule? monday = null,
        DaySchedule? tuesday = null,
        DaySchedule? wednesday = null,
        DaySchedule? thursday = null,
        DaySchedule? friday = null,
        DaySchedule? saturday = null,
        DaySchedule? sunday = null,
        DaySchedule? holiday = null,
        ScheduleType? type = null)
    {
        Name = DomainGuard.RequiredText(name, nameof(name));
        Type = InferAndValidateType(
            type,
            weekdays,
            weekends,
            monday,
            tuesday,
            wednesday,
            thursday,
            friday,
            saturday,
            sunday,
            holiday);

        Weekdays = weekdays ?? DaySchedule.Constant($"{Name}:weekdays", 0, Type);
        Weekends = weekends ?? DaySchedule.Constant($"{Name}:weekends", 0, Type);
        Monday = monday;
        Tuesday = tuesday;
        Wednesday = wednesday;
        Thursday = thursday;
        Friday = friday;
        Saturday = saturday;
        Sunday = sunday;
        Holiday = holiday;
    }

    public string Name { get; }

    public ScheduleType Type { get; }

    public DaySchedule Weekdays { get; }

    public DaySchedule Weekends { get; }

    public DaySchedule? Monday { get; }

    public DaySchedule? Tuesday { get; }

    public DaySchedule? Wednesday { get; }

    public DaySchedule? Thursday { get; }

    public DaySchedule? Friday { get; }

    public DaySchedule? Saturday { get; }

    public DaySchedule? Sunday { get; }

    public DaySchedule? Holiday { get; }

    public double Minimum => EffectiveDays().Min(day => day.Minimum);

    public double Maximum => EffectiveDays().Max(day => day.Maximum);

    public static RuleSet Constant(string name, double value, ScheduleType type = ScheduleType.Real)
    {
        DaySchedule day = DaySchedule.Constant($"{name}:day", value, type);
        return new RuleSet(name, day, day, type: type);
    }

    public static RuleSet FromDaySchedule(string name, DaySchedule day)
    {
        DomainGuard.NotNull(day, nameof(day));
        return new RuleSet(name, day, day, type: day.Type);
    }

    public DaySchedule GetDaySchedule(DayOfWeek dayOfWeek, bool isHoliday = false)
    {
        if (isHoliday)
        {
            return Holiday ?? Weekends;
        }

        return dayOfWeek switch
        {
            DayOfWeek.Monday => Monday ?? Weekdays,
            DayOfWeek.Tuesday => Tuesday ?? Weekdays,
            DayOfWeek.Wednesday => Wednesday ?? Weekdays,
            DayOfWeek.Thursday => Thursday ?? Weekdays,
            DayOfWeek.Friday => Friday ?? Weekdays,
            DayOfWeek.Saturday => Saturday ?? Weekends,
            DayOfWeek.Sunday => Sunday ?? Weekends,
            _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek), dayOfWeek, "Unknown day of week."),
        };
    }

    public RuleSet AsType(ScheduleType type)
    {
        return new RuleSet(
            Name,
            Weekdays.AsType(type),
            Weekends.AsType(type),
            Monday?.AsType(type),
            Tuesday?.AsType(type),
            Wednesday?.AsType(type),
            Thursday?.AsType(type),
            Friday?.AsType(type),
            Saturday?.AsType(type),
            Sunday?.AsType(type),
            Holiday?.AsType(type),
            type);
    }

    public RuleSet Multiply(RuleSet other)
    {
        return Combine(other, (left, right) => left * right, "MUL");
    }

    public RuleSet Add(RuleSet other)
    {
        return Combine(other, (left, right) => left + right, "ADD");
    }

    public RuleSet Subtract(RuleSet other)
    {
        return Combine(other, (left, right) => left - right, "SUB");
    }

    public RuleSet LogicalAnd(RuleSet other)
    {
        return Combine(other, (left, right) => left & right, "AND");
    }

    public RuleSet LogicalOr(RuleSet other)
    {
        return Combine(other, (left, right) => left | right, "OR");
    }

    public RuleSet Scale(double factor)
    {
        return Map(day => day * factor, $"{Name}:MUL:{factor}");
    }

    public RuleSet Clip(double? minimum = null, double? maximum = null, string? name = null)
    {
        return Map(day => day.Clip(minimum, maximum), name ?? $"{Name}:CLIP");
    }

    public bool Equals(RuleSet? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(Name, other.Name)
            && Type == other.Type
            && Equals(Weekdays, other.Weekdays)
            && Equals(Weekends, other.Weekends)
            && Equals(Monday, other.Monday)
            && Equals(Tuesday, other.Tuesday)
            && Equals(Wednesday, other.Wednesday)
            && Equals(Thursday, other.Thursday)
            && Equals(Friday, other.Friday)
            && Equals(Saturday, other.Saturday)
            && Equals(Sunday, other.Sunday)
            && Equals(Holiday, other.Holiday);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RuleSet);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(Name);
            hash = (hash * 397) ^ Type.GetHashCode();
            foreach (DaySchedule day in AllSlots())
            {
                hash = (hash * 397) ^ day.GetHashCode();
            }

            return hash;
        }
    }

    private static ScheduleType InferAndValidateType(ScheduleType? requested, params DaySchedule?[] days)
    {
        if (requested.HasValue && !Enum.IsDefined(typeof(ScheduleType), requested.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(requested), requested, "Unknown schedule type.");
        }

        DaySchedule? first = days.FirstOrDefault(day => day is not null);
        ScheduleType type = requested ?? first?.Type ?? ScheduleType.Real;
        if (requested.HasValue && first is not null && requested.Value != first.Type)
        {
            throw new ArgumentException($"The requested {requested.Value} type does not match supplied {first.Type} day schedules.");
        }

        if (days.Any(day => day is not null && day.Type != type))
        {
            throw new ArgumentException("A rule set cannot mix day schedule types.", nameof(days));
        }

        return type;
    }

    private IEnumerable<DaySchedule> EffectiveDays()
    {
        yield return GetDaySchedule(DayOfWeek.Monday);
        yield return GetDaySchedule(DayOfWeek.Tuesday);
        yield return GetDaySchedule(DayOfWeek.Wednesday);
        yield return GetDaySchedule(DayOfWeek.Thursday);
        yield return GetDaySchedule(DayOfWeek.Friday);
        yield return GetDaySchedule(DayOfWeek.Saturday);
        yield return GetDaySchedule(DayOfWeek.Sunday);
        yield return GetDaySchedule(DayOfWeek.Sunday, true);
    }

    private IEnumerable<DaySchedule> AllSlots()
    {
        yield return Weekdays;
        yield return Weekends;
        if (Monday is not null) yield return Monday;
        if (Tuesday is not null) yield return Tuesday;
        if (Wednesday is not null) yield return Wednesday;
        if (Thursday is not null) yield return Thursday;
        if (Friday is not null) yield return Friday;
        if (Saturday is not null) yield return Saturday;
        if (Sunday is not null) yield return Sunday;
        if (Holiday is not null) yield return Holiday;
    }

    private RuleSet Map(Func<DaySchedule, DaySchedule> operation, string name)
    {
        DaySchedule weekdays = operation(Weekdays);
        DaySchedule weekends = operation(Weekends);
        return new RuleSet(
            name,
            weekdays,
            weekends,
            Monday is null ? null : operation(Monday),
            Tuesday is null ? null : operation(Tuesday),
            Wednesday is null ? null : operation(Wednesday),
            Thursday is null ? null : operation(Thursday),
            Friday is null ? null : operation(Friday),
            Saturday is null ? null : operation(Saturday),
            Sunday is null ? null : operation(Sunday),
            Holiday is null ? null : operation(Holiday),
            weekdays.Type);
    }

    private RuleSet Combine(
        RuleSet other,
        Func<DaySchedule, DaySchedule, DaySchedule> operation,
        string operationName)
    {
        DomainGuard.NotNull(other, nameof(other));
        DaySchedule weekdays = operation(Weekdays, other.Weekdays);
        DaySchedule weekends = operation(Weekends, other.Weekends);
        return new RuleSet(
            $"{Name}:{operationName}:{other.Name}",
            weekdays,
            weekends,
            CombineOverride(Monday, other.Monday, DayOfWeek.Monday, other, operation),
            CombineOverride(Tuesday, other.Tuesday, DayOfWeek.Tuesday, other, operation),
            CombineOverride(Wednesday, other.Wednesday, DayOfWeek.Wednesday, other, operation),
            CombineOverride(Thursday, other.Thursday, DayOfWeek.Thursday, other, operation),
            CombineOverride(Friday, other.Friday, DayOfWeek.Friday, other, operation),
            CombineOverride(Saturday, other.Saturday, DayOfWeek.Saturday, other, operation),
            CombineOverride(Sunday, other.Sunday, DayOfWeek.Sunday, other, operation),
            Holiday is null && other.Holiday is null
                ? null
                : operation(GetDaySchedule(DayOfWeek.Sunday, true), other.GetDaySchedule(DayOfWeek.Sunday, true)),
            weekdays.Type);
    }

    private DaySchedule? CombineOverride(
        DaySchedule? left,
        DaySchedule? right,
        DayOfWeek day,
        RuleSet other,
        Func<DaySchedule, DaySchedule, DaySchedule> operation)
    {
        return left is null && right is null
            ? null
            : operation(GetDaySchedule(day), other.GetDaySchedule(day));
    }
}
