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

    public double Minimum => AllSlots().Min(day => day.Minimum);

    public double Maximum => AllSlots().Max(day => day.Maximum);

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

    public RuleSet Multiply(double value)
    {
        return Map(day => day * value, $"{Name}:MUL:{value}");
    }

    public RuleSet Add(RuleSet other)
    {
        return Combine(other, (left, right) => left + right, "ADD");
    }

    public RuleSet Add(double value)
    {
        return Map(day => day + value, $"{Name}:ADD:{value}");
    }

    public RuleSet ReverseAdd(double value)
    {
        return Map(day => value + day, $"{value}:ADD:{Name}");
    }

    public RuleSet Subtract(RuleSet other)
    {
        return Combine(other, (left, right) => left - right, "SUB");
    }

    public RuleSet Subtract(double value)
    {
        return Map(day => day - value, $"{Name}:SUB:{value}");
    }

    public RuleSet ReverseSubtract(double value)
    {
        return Map(day => value - day, $"{value}:SUB:{Name}");
    }

    public RuleSet Divide(RuleSet other)
    {
        return Combine(other, (left, right) => left / right, "DIV");
    }

    public RuleSet Divide(double value)
    {
        return Map(day => day / value, $"{Name}:DIV:{value}");
    }

    public RuleSet ReverseDivide(double value)
    {
        return Map(day => value / day, $"{value}:DIV:{Name}");
    }

    public RuleSet LogicalAnd(RuleSet other)
    {
        return Combine(other, (left, right) => left & right, "AND");
    }

    public RuleSet LogicalOr(RuleSet other)
    {
        return Combine(other, (left, right) => left | right, "OR");
    }

    public RuleSet Invert()
    {
        return Map(day => !day, Name);
    }

    public RuleSet ElementEqual(RuleSet other)
    {
        return Combine(other, (left, right) => left.ElementEqual(right), "EQ");
    }

    public RuleSet ElementEqual(double value)
    {
        return Map(day => day.ElementEqual(value), $"{Name}:EQ:{value}");
    }

    public RuleSet ElementNotEqual(RuleSet other)
    {
        return Combine(other, (left, right) => left.ElementNotEqual(right), "NE");
    }

    public RuleSet ElementNotEqual(double value)
    {
        return Map(day => day.ElementNotEqual(value), $"{Name}:NE:{value}");
    }

    public RuleSet LessThan(RuleSet other)
    {
        return Combine(other, (left, right) => left.LessThan(right), "LT");
    }

    public RuleSet LessThan(double value)
    {
        return Map(day => day.LessThan(value), $"{Name}:LT:{value}");
    }

    public RuleSet LessThanOrEqual(RuleSet other)
    {
        return Combine(other, (left, right) => left.LessThanOrEqual(right), "LE");
    }

    public RuleSet LessThanOrEqual(double value)
    {
        return Map(day => day.LessThanOrEqual(value), $"{Name}:LE:{value}");
    }

    public RuleSet GreaterThan(RuleSet other)
    {
        return Combine(other, (left, right) => left.GreaterThan(right), "GT");
    }

    public RuleSet GreaterThan(double value)
    {
        return Map(day => day.GreaterThan(value), $"{Name}:GT:{value}");
    }

    public RuleSet GreaterThanOrEqual(RuleSet other)
    {
        return Combine(other, (left, right) => left.GreaterThanOrEqual(right), "GE");
    }

    public RuleSet GreaterThanOrEqual(double value)
    {
        return Map(day => day.GreaterThanOrEqual(value), $"{Name}:GE:{value}");
    }

    public RuleSet ElementMinimum(RuleSet other)
    {
        return Combine(other, (left, right) => left.ElementMinimum(right), "MIN");
    }

    public RuleSet ElementMinimum(double value)
    {
        return Map(day => day.ElementMinimum(value), $"{Name}:MIN:{value}");
    }

    public RuleSet ElementMaximum(RuleSet other)
    {
        return Combine(other, (left, right) => left.ElementMaximum(right), "MAX");
    }

    public RuleSet ElementMaximum(double value)
    {
        return Map(day => day.ElementMaximum(value), $"{Name}:MAX:{value}");
    }

    public RuleSet IsOn()
    {
        return ElementEqual(1);
    }

    public RuleSet IsOff()
    {
        return ElementEqual(0);
    }

    public RuleSet IsPositive()
    {
        return GreaterThan(0);
    }

    public RuleSet IsNegative()
    {
        return LessThan(0);
    }

    public RuleSet IsZero()
    {
        return ElementEqual(0);
    }

    public RuleSet IsNonzero()
    {
        return ElementNotEqual(0);
    }

    public RuleSet IsBetween(
        double minimum,
        double maximum,
        bool includeMinimum = true,
        bool includeMaximum = true)
    {
        RuleSet lower = includeMinimum ? GreaterThanOrEqual(minimum) : GreaterThan(minimum);
        RuleSet upper = includeMaximum ? LessThanOrEqual(maximum) : LessThan(maximum);
        return lower.LogicalAnd(upper);
    }

    public static RuleSet Where(
        RuleSet condition,
        object whenTrue,
        object whenFalse,
        string? name = null,
        ScheduleType? type = null)
    {
        DomainGuard.NotNull(condition, nameof(condition));
        DomainGuard.NotNull(whenTrue, nameof(whenTrue));
        DomainGuard.NotNull(whenFalse, nameof(whenFalse));
        if (condition.Type != ScheduleType.OnOff)
        {
            throw new ScheduleOperationException("RuleSet.Where requires an OnOff condition rule set.");
        }

        RuleSet trueRuleSet = CoerceWhereValue(whenTrue, type, nameof(whenTrue));
        RuleSet falseRuleSet = CoerceWhereValue(whenFalse, type, nameof(whenFalse));
        string resultName = name ?? "WHERE";
        DaySchedule weekdays = DaySchedule.Where(
            condition.Weekdays,
            trueRuleSet.Weekdays,
            falseRuleSet.Weekdays,
            $"{resultName}:weekdays",
            type: type);
        DaySchedule weekends = DaySchedule.Where(
            condition.Weekends,
            trueRuleSet.Weekends,
            falseRuleSet.Weekends,
            $"{resultName}:weekends",
            type: type);

        return new RuleSet(
            resultName,
            weekdays,
            weekends,
            WhereOverride(condition.Monday, trueRuleSet.Monday, falseRuleSet.Monday, condition.Weekdays, trueRuleSet.Weekdays, falseRuleSet.Weekdays, $"{resultName}:monday", type),
            WhereOverride(condition.Tuesday, trueRuleSet.Tuesday, falseRuleSet.Tuesday, condition.Weekdays, trueRuleSet.Weekdays, falseRuleSet.Weekdays, $"{resultName}:tuesday", type),
            WhereOverride(condition.Wednesday, trueRuleSet.Wednesday, falseRuleSet.Wednesday, condition.Weekdays, trueRuleSet.Weekdays, falseRuleSet.Weekdays, $"{resultName}:wednesday", type),
            WhereOverride(condition.Thursday, trueRuleSet.Thursday, falseRuleSet.Thursday, condition.Weekdays, trueRuleSet.Weekdays, falseRuleSet.Weekdays, $"{resultName}:thursday", type),
            WhereOverride(condition.Friday, trueRuleSet.Friday, falseRuleSet.Friday, condition.Weekdays, trueRuleSet.Weekdays, falseRuleSet.Weekdays, $"{resultName}:friday", type),
            WhereOverride(condition.Saturday, trueRuleSet.Saturday, falseRuleSet.Saturday, condition.Weekends, trueRuleSet.Weekends, falseRuleSet.Weekends, $"{resultName}:saturday", type),
            WhereOverride(condition.Sunday, trueRuleSet.Sunday, falseRuleSet.Sunday, condition.Weekends, trueRuleSet.Weekends, falseRuleSet.Weekends, $"{resultName}:sunday", type),
            WhereOverride(condition.Holiday, trueRuleSet.Holiday, falseRuleSet.Holiday, condition.Weekends, trueRuleSet.Weekends, falseRuleSet.Weekends, $"{resultName}:holiday", type),
            weekdays.Type);
    }

    public RuleSet Scale(double factor)
    {
        return Multiply(factor);
    }

    public RuleSet NormalizeByMaximum(string? name = null)
    {
        return Map(
            day => day.NormalizeByMaximum(),
            name ?? $"{Name}_normalized");
    }

    public RuleSet Clip(double? minimum = null, double? maximum = null, string? name = null)
    {
        return Map(day => day.Clip(minimum, maximum), name ?? $"{Name}:CLIP");
    }

    public static RuleSet operator *(RuleSet left, RuleSet right)
    {
        return left.Multiply(right);
    }

    public static RuleSet operator *(RuleSet ruleSet, double value)
    {
        return ruleSet.Multiply(value);
    }

    public static RuleSet operator *(double value, RuleSet ruleSet)
    {
        return ruleSet.Multiply(value);
    }

    public static RuleSet operator /(RuleSet left, RuleSet right)
    {
        return left.Divide(right);
    }

    public static RuleSet operator /(RuleSet ruleSet, double value)
    {
        return ruleSet.Divide(value);
    }

    public static RuleSet operator /(double value, RuleSet ruleSet)
    {
        return ruleSet.ReverseDivide(value);
    }

    public static RuleSet operator +(RuleSet left, RuleSet right)
    {
        return left.Add(right);
    }

    public static RuleSet operator +(RuleSet ruleSet, double value)
    {
        return ruleSet.Add(value);
    }

    public static RuleSet operator +(double value, RuleSet ruleSet)
    {
        return ruleSet.ReverseAdd(value);
    }

    public static RuleSet operator -(RuleSet left, RuleSet right)
    {
        return left.Subtract(right);
    }

    public static RuleSet operator -(RuleSet ruleSet, double value)
    {
        return ruleSet.Subtract(value);
    }

    public static RuleSet operator -(double value, RuleSet ruleSet)
    {
        return ruleSet.ReverseSubtract(value);
    }

    public static RuleSet operator &(RuleSet left, RuleSet right)
    {
        return left.LogicalAnd(right);
    }

    public static RuleSet operator |(RuleSet left, RuleSet right)
    {
        return left.LogicalOr(right);
    }

    public static RuleSet operator !(RuleSet ruleSet)
    {
        return ruleSet.Invert();
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

    private static RuleSet CoerceWhereValue(object value, ScheduleType? type, string parameterName)
    {
        if (value is RuleSet ruleSet)
        {
            return ruleSet;
        }

        if (value is DaySchedule daySchedule)
        {
            return FromDaySchedule("WHERE", daySchedule);
        }

        if (TryGetScalar(value, out double scalar))
        {
            return Constant("WHERE", scalar, type ?? ScheduleType.Real);
        }

        throw new ArgumentException(
            "A conditional value must be numeric, a DaySchedule, or a RuleSet.",
            parameterName);
    }

    private static bool TryGetScalar(object value, out double scalar)
    {
        switch (value)
        {
            case byte number: scalar = number; return true;
            case sbyte number: scalar = number; return true;
            case short number: scalar = number; return true;
            case ushort number: scalar = number; return true;
            case int number: scalar = number; return true;
            case uint number: scalar = number; return true;
            case long number: scalar = number; return true;
            case ulong number: scalar = number; return true;
            case float number: scalar = number; return true;
            case double number: scalar = number; return true;
            case decimal number: scalar = (double)number; return true;
            default: scalar = default; return false;
        }
    }

    private static DaySchedule? WhereOverride(
        DaySchedule? conditionOverride,
        DaySchedule? trueOverride,
        DaySchedule? falseOverride,
        DaySchedule conditionFallback,
        DaySchedule trueFallback,
        DaySchedule falseFallback,
        string name,
        ScheduleType? type)
    {
        if (conditionOverride is null && trueOverride is null && falseOverride is null)
        {
            return null;
        }

        return DaySchedule.Where(
            conditionOverride ?? conditionFallback,
            trueOverride ?? trueFallback,
            falseOverride ?? falseFallback,
            name,
            type: type);
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
