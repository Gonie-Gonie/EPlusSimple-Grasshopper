using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Profile;

/// <summary>
/// Immutable weekday/weekend defaults with optional day-specific overrides.
/// </summary>
public sealed class RuleSet : IEquatable<RuleSet>
{
    private static readonly string[] DayKeys =
    {
        "monday",
        "tuesday",
        "wednesday",
        "thursday",
        "friday",
        "saturday",
        "sunday",
        "holiday",
    };

    public RuleSet(
        string? name,
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
        Name = NormalizeName(name);
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

    /// <summary>
    /// Creates a value-equivalent independent copy of every populated day slot.
    /// Repeated references in the source are intentionally split, matching the
    /// pinned Python <c>__deepcopy__</c> implementation's per-slot calls.
    /// </summary>
    public RuleSet DeepCopy()
    {
        return new RuleSet(
            $"{Name}:COPY",
            Weekdays.DeepCopy(),
            Weekends.DeepCopy(),
            Monday?.DeepCopy(),
            Tuesday?.DeepCopy(),
            Wednesday?.DeepCopy(),
            Thursday?.DeepCopy(),
            Friday?.DeepCopy(),
            Saturday?.DeepCopy(),
            Sunday?.DeepCopy(),
            Holiday?.DeepCopy(),
            Type);
    }

    public static RuleSet Constant(string name, double value, ScheduleType type = ScheduleType.Real)
    {
        DaySchedule day = DaySchedule.ConstantFromPythonScalar($"{name}:day", value, type);
        return new RuleSet(name, day, day, type: type);
    }

    public static RuleSet Constant<T>(string name, T value, ScheduleType type = ScheduleType.Real)
    {
        DaySchedule day = DaySchedule.ConstantFromPythonScalar($"{name}:day", value, type);
        return new RuleSet(name, day, day, type: type);
    }

    public static RuleSet FromDaySchedule(string name, DaySchedule day)
    {
        DomainGuard.NotNull(day, nameof(day));
        return new RuleSet(name, day, day, type: day.Type);
    }

    /// <summary>
    /// Creates a rule set from a Python-compatible scalar. Scalar values create
    /// distinct weekday and weekend schedules, as in the pinned implementation.
    /// </summary>
    public static RuleSet FromConstant<T>(
        string? name,
        T value,
        ScheduleType? type = null)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value is DaySchedule daySchedule)
        {
            return FromConstant(name, daySchedule, type);
        }

        string ruleSetName = NormalizeName(name);
        ScheduleType resultType = ValidateScheduleType(type ?? ScheduleType.Real, nameof(type));
        DaySchedule weekdays = DaySchedule.ConstantFromPythonScalar(
            $"{ruleSetName}:weekdays",
            value,
            resultType);
        DaySchedule weekends = DaySchedule.ConstantFromPythonScalar(
            $"{ruleSetName}:weekends",
            value,
            resultType);
        return new RuleSet(ruleSetName, weekdays, weekends, type: resultType);
    }

    /// <summary>
    /// Creates a rule set whose defaults reference the supplied day schedule.
    /// The optional type is ignored to preserve the upstream typed-day factory
    /// contract; the day schedule is authoritative.
    /// </summary>
    public static RuleSet FromConstant(
        string? name,
        DaySchedule value,
        ScheduleType? type = null)
    {
        DomainGuard.NotNull(value, nameof(value));
        return new RuleSet(name, value, value, type: value.Type);
    }

    /// <summary>
    /// Creates a rule set from a shared default and optional per-day overrides.
    /// Inputs may be day schedules or Python-compatible scalar values.
    /// </summary>
    public static RuleSet FromDays(
        string? name,
        object defaultValue,
        object? monday = null,
        object? tuesday = null,
        object? wednesday = null,
        object? thursday = null,
        object? friday = null,
        object? saturday = null,
        object? sunday = null,
        object? holiday = null,
        ScheduleType? type = null)
    {
        DomainGuard.NotNull(defaultValue, nameof(defaultValue));

        string ruleSetName = NormalizeName(name);
        ScheduleType inferredType = defaultValue is DaySchedule defaultSchedule
            ? defaultSchedule.Type
            : ValidateScheduleType(type ?? ScheduleType.Real, nameof(type));
        DaySchedule defaultDay = CoerceDaySchedule(
            defaultValue,
            inferredType,
            $"{ruleSetName}:default",
            nameof(defaultValue));

        return new RuleSet(
            ruleSetName,
            defaultDay,
            defaultDay,
            CoerceOptionalDaySchedule(monday, inferredType, $"{ruleSetName}:monday", nameof(monday)),
            CoerceOptionalDaySchedule(tuesday, inferredType, $"{ruleSetName}:tuesday", nameof(tuesday)),
            CoerceOptionalDaySchedule(wednesday, inferredType, $"{ruleSetName}:wednesday", nameof(wednesday)),
            CoerceOptionalDaySchedule(thursday, inferredType, $"{ruleSetName}:thursday", nameof(thursday)),
            CoerceOptionalDaySchedule(friday, inferredType, $"{ruleSetName}:friday", nameof(friday)),
            CoerceOptionalDaySchedule(saturday, inferredType, $"{ruleSetName}:saturday", nameof(saturday)),
            CoerceOptionalDaySchedule(sunday, inferredType, $"{ruleSetName}:sunday", nameof(sunday)),
            CoerceOptionalDaySchedule(holiday, inferredType, $"{ruleSetName}:holiday", nameof(holiday)),
            inferredType);
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

    /// <summary>
    /// Resolves one of the eight optional day keys, optionally returning the raw
    /// nullable override instead of its weekday/weekend fallback.
    /// </summary>
    public DaySchedule? GetDaySchedule(string key, bool fallback = true)
    {
        DomainGuard.NotNull(key, nameof(key));

        if (key == "weekdays")
        {
            return Weekdays;
        }

        if (key == "weekends")
        {
            return Weekends;
        }

        DaySchedule? explicitDay = GetExplicitDay(key);
        if (explicitDay is not null || !fallback)
        {
            return explicitDay;
        }

        return IsWeekdayKey(key) ? Weekdays : Weekends;
    }

    /// <summary>
    /// Resolves the pinned Monday-through-Holiday index order. Python-compatible
    /// negative indices -8 through -1 are supported.
    /// </summary>
    public DaySchedule? GetDaySchedule(int index, bool fallback = true)
    {
        int normalized = index < 0 ? index + DayKeys.Length : index;
        if (normalized < 0 || normalized >= DayKeys.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                $"A rule-set day index must be between {-DayKeys.Length} and {DayKeys.Length - 1}.");
        }

        return GetDaySchedule(DayKeys[normalized], fallback);
    }

    /// <summary>
    /// Returns a new rule set with one slot replaced. Defaults cannot be cleared;
    /// optional day overrides may be set to <see langword="null"/>.
    /// </summary>
    public RuleSet WithDaySchedule(string key, DaySchedule? value)
    {
        DomainGuard.NotNull(key, nameof(key));

        if ((key == "weekdays" || key == "weekends") && value is null)
        {
            throw new ArgumentNullException(nameof(value), $"The {key} default cannot be null.");
        }

        if (value is not null && value.Type != Type)
        {
            throw new ArgumentException(
                $"DaySchedule type mismatch: expected {Type.CanonicalName()}, got {value.Type.CanonicalName()}.",
                nameof(value));
        }

        return key switch
        {
            "weekdays" => Reconstruct(weekdays: value),
            "weekends" => Reconstruct(weekends: value),
            "monday" => Reconstruct(monday: value, replaceMonday: true),
            "tuesday" => Reconstruct(tuesday: value, replaceTuesday: true),
            "wednesday" => Reconstruct(wednesday: value, replaceWednesday: true),
            "thursday" => Reconstruct(thursday: value, replaceThursday: true),
            "friday" => Reconstruct(friday: value, replaceFriday: true),
            "saturday" => Reconstruct(saturday: value, replaceSaturday: true),
            "sunday" => Reconstruct(sunday: value, replaceSunday: true),
            "holiday" => Reconstruct(holiday: value, replaceHoliday: true),
            _ => throw UnknownDayKey(key, nameof(key)),
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
        return MultiplyPythonScalar(value);
    }

    public RuleSet Multiply<T>(T value)
    {
        return MultiplyPythonScalar(value);
    }

    public RuleSet Add(RuleSet other)
    {
        return Combine(other, (left, right) => left + right, "ADD");
    }

    public RuleSet Add(double value)
    {
        return AddPythonScalar(value);
    }

    public RuleSet Add<T>(T value)
    {
        return AddPythonScalar(value);
    }

    public RuleSet ReverseAdd(double value)
    {
        return ReverseAddPythonScalar(value);
    }

    public RuleSet ReverseAdd<T>(T value)
    {
        return ReverseAddPythonScalar(value);
    }

    public RuleSet Subtract(RuleSet other)
    {
        return Combine(other, (left, right) => left - right, "SUB");
    }

    public RuleSet Subtract(double value)
    {
        return SubtractPythonScalar(value);
    }

    public RuleSet Subtract<T>(T value)
    {
        return SubtractPythonScalar(value);
    }

    public RuleSet ReverseSubtract(double value)
    {
        return ReverseSubtractPythonScalar(value);
    }

    public RuleSet ReverseSubtract<T>(T value)
    {
        return ReverseSubtractPythonScalar(value);
    }

    public RuleSet Divide(RuleSet other)
    {
        return Combine(other, (left, right) => left / right, "DIV");
    }

    public RuleSet Divide(double value)
    {
        return DividePythonScalar(value);
    }

    public RuleSet Divide<T>(T value)
    {
        return DividePythonScalar(value);
    }

    public RuleSet ReverseDivide(double value)
    {
        return ReverseDividePythonScalar(value);
    }

    public RuleSet ReverseDivide<T>(T value)
    {
        return ReverseDividePythonScalar(value);
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
        return ElementEqualPythonScalar(value);
    }

    public RuleSet ElementEqual<T>(T value)
    {
        return ElementEqualPythonScalar(value);
    }

    public RuleSet ElementNotEqual(RuleSet other)
    {
        return Combine(other, (left, right) => left.ElementNotEqual(right), "NE");
    }

    public RuleSet ElementNotEqual(double value)
    {
        return ElementNotEqualPythonScalar(value);
    }

    public RuleSet ElementNotEqual<T>(T value)
    {
        return ElementNotEqualPythonScalar(value);
    }

    public RuleSet LessThan(RuleSet other)
    {
        return Combine(other, (left, right) => left.LessThan(right), "LT");
    }

    public RuleSet LessThan(double value)
    {
        return LessThanPythonScalar(value);
    }

    public RuleSet LessThan<T>(T value)
    {
        return LessThanPythonScalar(value);
    }

    public RuleSet LessThanOrEqual(RuleSet other)
    {
        return Combine(other, (left, right) => left.LessThanOrEqual(right), "LE");
    }

    public RuleSet LessThanOrEqual(double value)
    {
        return LessThanOrEqualPythonScalar(value);
    }

    public RuleSet LessThanOrEqual<T>(T value)
    {
        return LessThanOrEqualPythonScalar(value);
    }

    public RuleSet GreaterThan(RuleSet other)
    {
        return Combine(other, (left, right) => left.GreaterThan(right), "GT");
    }

    public RuleSet GreaterThan(double value)
    {
        return GreaterThanPythonScalar(value);
    }

    public RuleSet GreaterThan<T>(T value)
    {
        return GreaterThanPythonScalar(value);
    }

    public RuleSet GreaterThanOrEqual(RuleSet other)
    {
        return Combine(other, (left, right) => left.GreaterThanOrEqual(right), "GE");
    }

    public RuleSet GreaterThanOrEqual(double value)
    {
        return GreaterThanOrEqualPythonScalar(value);
    }

    public RuleSet GreaterThanOrEqual<T>(T value)
    {
        return GreaterThanOrEqualPythonScalar(value);
    }

    public RuleSet ElementMinimum(RuleSet other)
    {
        return Combine(other, (left, right) => left.ElementMinimum(right), "MIN");
    }

    public RuleSet ElementMinimum(double value)
    {
        return ElementMinimumPythonScalar(value);
    }

    public RuleSet ElementMinimum<T>(T value)
    {
        return ElementMinimumPythonScalar(value);
    }

    public RuleSet ElementMaximum(RuleSet other)
    {
        return Combine(other, (left, right) => left.ElementMaximum(right), "MAX");
    }

    public RuleSet ElementMaximum(double value)
    {
        return ElementMaximumPythonScalar(value);
    }

    public RuleSet ElementMaximum<T>(T value)
    {
        return ElementMaximumPythonScalar(value);
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

    public RuleSet IsBetween<TMinimum, TMaximum>(
        TMinimum minimum,
        TMaximum maximum,
        bool includeMinimum = true,
        bool includeMaximum = true)
    {
        RuleSet lower = includeMinimum
            ? GreaterThanOrEqualPythonScalar(minimum)
            : GreaterThanPythonScalar(minimum);
        RuleSet upper = includeMaximum
            ? LessThanOrEqualPythonScalar(maximum)
            : LessThanPythonScalar(maximum);
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

        WhereOperand trueValue = CoerceWhereValue(whenTrue, type, nameof(whenTrue));
        WhereOperand falseValue = CoerceWhereValue(whenFalse, type, nameof(whenFalse));
        ScheduleType resultType = type ?? trueValue.Type;
        if (trueValue.Type != resultType || falseValue.Type != resultType)
        {
            throw new ScheduleOperationException(
                "RuleSet.Where result branches must have the same requested schedule type.");
        }

        string resultName;
        if (name is null || name.Length == 0)
        {
            resultName = "WHERE";
        }
        else
        {
            resultName = DomainGuard.RequiredText(name, nameof(name));
        }
        DaySchedule weekdays = SelectWhereDay(
            condition.Weekdays,
            trueValue.Weekdays,
            falseValue.Weekdays,
            $"{resultName}:weekdays",
            resultType);
        DaySchedule weekends = SelectWhereDay(
            condition.Weekends,
            trueValue.Weekends,
            falseValue.Weekends,
            $"{resultName}:weekends",
            resultType);

        return new RuleSet(
            resultName,
            weekdays,
            weekends,
            WhereOverride(condition.Monday, trueValue.Monday, falseValue.Monday, condition.Weekdays, trueValue.Weekdays, falseValue.Weekdays, $"{resultName}:monday", resultType),
            WhereOverride(condition.Tuesday, trueValue.Tuesday, falseValue.Tuesday, condition.Weekdays, trueValue.Weekdays, falseValue.Weekdays, $"{resultName}:tuesday", resultType),
            WhereOverride(condition.Wednesday, trueValue.Wednesday, falseValue.Wednesday, condition.Weekdays, trueValue.Weekdays, falseValue.Weekdays, $"{resultName}:wednesday", resultType),
            WhereOverride(condition.Thursday, trueValue.Thursday, falseValue.Thursday, condition.Weekdays, trueValue.Weekdays, falseValue.Weekdays, $"{resultName}:thursday", resultType),
            WhereOverride(condition.Friday, trueValue.Friday, falseValue.Friday, condition.Weekdays, trueValue.Weekdays, falseValue.Weekdays, $"{resultName}:friday", resultType),
            WhereOverride(condition.Saturday, trueValue.Saturday, falseValue.Saturday, condition.Weekends, trueValue.Weekends, falseValue.Weekends, $"{resultName}:saturday", resultType),
            WhereOverride(condition.Sunday, trueValue.Sunday, falseValue.Sunday, condition.Weekends, trueValue.Weekends, falseValue.Weekends, $"{resultName}:sunday", resultType),
            WhereOverride(condition.Holiday, trueValue.Holiday, falseValue.Holiday, condition.Weekends, trueValue.Weekends, falseValue.Weekends, $"{resultName}:holiday", resultType),
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
        return Map(
            day => day.Clip(minimum, maximum),
            string.IsNullOrEmpty(name) ? $"{Name}:CLIP" : name!);
    }

    /// <summary>
    /// Returns all ten slots in the pinned upstream insertion order.
    /// </summary>
    public IReadOnlyDictionary<string, DaySchedule?> ToDictionary()
    {
        var slots = new Dictionary<string, DaySchedule?>(StringComparer.Ordinal)
        {
            ["weekdays"] = Weekdays,
            ["weekends"] = Weekends,
            ["monday"] = Monday,
            ["tuesday"] = Tuesday,
            ["wednesday"] = Wednesday,
            ["thursday"] = Thursday,
            ["friday"] = Friday,
            ["saturday"] = Saturday,
            ["sunday"] = Sunday,
            ["holiday"] = Holiday,
        };
        return new ReadOnlyDictionary<string, DaySchedule?>(slots);
    }

    public string Summary(bool includeDays = true)
    {
        string[] overrideKeys = DayKeys
            .Where(key => GetExplicitDay(key) is not null)
            .ToArray();
        var lines = new List<string>
        {
            $"RuleSet {PythonRepr(Name)} [type={Type.CanonicalName()}]",
            $"  range: min={FormatPythonGeneral(Minimum)}, max={FormatPythonGeneral(Maximum)}",
            $"  defaults: weekdays={PythonRepr(Weekdays.Name)}, weekends={PythonRepr(Weekends.Name)}",
            $"  overrides: {(overrideKeys.Length == 0 ? "none" : string.Join(", ", overrideKeys))}",
        };

        if (includeDays)
        {
            foreach (string key in DayKeys)
            {
                DaySchedule? explicitDay = GetExplicitDay(key);
                DaySchedule effective = GetDaySchedule(key, fallback: true)!;
                string source = explicitDay is null ? "fallback" : "override";
                lines.Add(
                    $"  {key,-9}: {PythonRepr(effective.Name)} "
                        + $"({source}, min={FormatPythonGeneral(effective.Minimum)}, "
                        + $"max={FormatPythonGeneral(effective.Maximum)})");
            }
        }

        return string.Join("\n", lines);
    }

    public override string ToString()
    {
        return Summary();
    }

    public IReadOnlyList<string> ToIdfCompactExpression()
    {
        var fields = new List<string>();
        bool hasWeekdayOverride = Monday is not null
            || Tuesday is not null
            || Wednesday is not null
            || Thursday is not null
            || Friday is not null;
        if (hasWeekdayOverride)
        {
            AppendDayIdfFields(fields, "Monday", Monday ?? Weekdays);
            AppendDayIdfFields(fields, "Tuesday", Tuesday ?? Weekdays);
            AppendDayIdfFields(fields, "Wednesday", Wednesday ?? Weekdays);
            AppendDayIdfFields(fields, "Thursday", Thursday ?? Weekdays);
            AppendDayIdfFields(fields, "Friday", Friday ?? Weekdays);
        }
        else
        {
            AppendDayIdfFields(fields, "Weekdays", Weekdays);
        }

        bool hasWeekendOverride = Saturday is not null || Sunday is not null;
        if (hasWeekendOverride)
        {
            AppendDayIdfFields(fields, "Saturday", Saturday ?? Weekends);
            AppendDayIdfFields(fields, "Sunday", Sunday ?? Weekends);
        }
        else
        {
            AppendDayIdfFields(fields, "Weekends", Weekends);
        }

        if (Holiday is not null)
        {
            AppendDayIdfFields(fields, "Holiday", Holiday);
        }

        AppendDayIdfFields(fields, "AllOtherDays", Weekends);
        return new ReadOnlyCollection<string>(fields);
    }

    public static RuleSet operator *(RuleSet left, RuleSet right)
    {
        return left.Multiply(right);
    }

    public static RuleSet operator *(RuleSet ruleSet, double value)
    {
        return ruleSet.Multiply(value);
    }

    public static RuleSet operator *(RuleSet ruleSet, int value) => ruleSet.Multiply(value);

    public static RuleSet operator *(RuleSet ruleSet, uint value) => ruleSet.Multiply(value);

    public static RuleSet operator *(RuleSet ruleSet, long value) => ruleSet.Multiply(value);

    public static RuleSet operator *(RuleSet ruleSet, ulong value) => ruleSet.Multiply(value);

    public static RuleSet operator *(RuleSet ruleSet, BigInteger value) => ruleSet.Multiply(value);

    public static RuleSet operator *(RuleSet ruleSet, bool value) => ruleSet.Multiply(value);

    public static RuleSet operator *(RuleSet ruleSet, char value) => ruleSet.Multiply(value);

    public static RuleSet operator *(double value, RuleSet ruleSet)
    {
        return ruleSet.Multiply(value);
    }

    public static RuleSet operator *(int value, RuleSet ruleSet) => ruleSet.Multiply(value);

    public static RuleSet operator *(uint value, RuleSet ruleSet) => ruleSet.Multiply(value);

    public static RuleSet operator *(long value, RuleSet ruleSet) => ruleSet.Multiply(value);

    public static RuleSet operator *(ulong value, RuleSet ruleSet) => ruleSet.Multiply(value);

    public static RuleSet operator *(BigInteger value, RuleSet ruleSet) => ruleSet.Multiply(value);

    public static RuleSet operator *(bool value, RuleSet ruleSet) => ruleSet.Multiply(value);

    public static RuleSet operator *(char value, RuleSet ruleSet) => ruleSet.Multiply(value);

    public static RuleSet operator /(RuleSet left, RuleSet right)
    {
        return left.Divide(right);
    }

    public static RuleSet operator /(RuleSet ruleSet, double value)
    {
        return ruleSet.Divide(value);
    }

    public static RuleSet operator /(RuleSet ruleSet, int value) => ruleSet.Divide(value);

    public static RuleSet operator /(RuleSet ruleSet, uint value) => ruleSet.Divide(value);

    public static RuleSet operator /(RuleSet ruleSet, long value) => ruleSet.Divide(value);

    public static RuleSet operator /(RuleSet ruleSet, ulong value) => ruleSet.Divide(value);

    public static RuleSet operator /(RuleSet ruleSet, BigInteger value) => ruleSet.Divide(value);

    public static RuleSet operator /(RuleSet ruleSet, bool value) => ruleSet.Divide(value);

    public static RuleSet operator /(RuleSet ruleSet, char value) => ruleSet.Divide(value);

    public static RuleSet operator /(double value, RuleSet ruleSet)
    {
        return ruleSet.ReverseDivide(value);
    }

    public static RuleSet operator /(int value, RuleSet ruleSet) => ruleSet.ReverseDivide(value);

    public static RuleSet operator /(uint value, RuleSet ruleSet) => ruleSet.ReverseDivide(value);

    public static RuleSet operator /(long value, RuleSet ruleSet) => ruleSet.ReverseDivide(value);

    public static RuleSet operator /(ulong value, RuleSet ruleSet) => ruleSet.ReverseDivide(value);

    public static RuleSet operator /(BigInteger value, RuleSet ruleSet) => ruleSet.ReverseDivide(value);

    public static RuleSet operator /(bool value, RuleSet ruleSet) => ruleSet.ReverseDivide(value);

    public static RuleSet operator /(char value, RuleSet ruleSet) => ruleSet.ReverseDivide(value);

    public static RuleSet operator +(RuleSet left, RuleSet right)
    {
        return left.Add(right);
    }

    public static RuleSet operator +(RuleSet ruleSet, double value)
    {
        return ruleSet.Add(value);
    }

    public static RuleSet operator +(RuleSet ruleSet, int value) => ruleSet.Add(value);

    public static RuleSet operator +(RuleSet ruleSet, uint value) => ruleSet.Add(value);

    public static RuleSet operator +(RuleSet ruleSet, long value) => ruleSet.Add(value);

    public static RuleSet operator +(RuleSet ruleSet, ulong value) => ruleSet.Add(value);

    public static RuleSet operator +(RuleSet ruleSet, BigInteger value) => ruleSet.Add(value);

    public static RuleSet operator +(RuleSet ruleSet, bool value) => ruleSet.Add(value);

    public static RuleSet operator +(RuleSet ruleSet, char value) => ruleSet.Add(value);

    public static RuleSet operator +(double value, RuleSet ruleSet)
    {
        return ruleSet.ReverseAdd(value);
    }

    public static RuleSet operator +(int value, RuleSet ruleSet) => ruleSet.ReverseAdd(value);

    public static RuleSet operator +(uint value, RuleSet ruleSet) => ruleSet.ReverseAdd(value);

    public static RuleSet operator +(long value, RuleSet ruleSet) => ruleSet.ReverseAdd(value);

    public static RuleSet operator +(ulong value, RuleSet ruleSet) => ruleSet.ReverseAdd(value);

    public static RuleSet operator +(BigInteger value, RuleSet ruleSet) => ruleSet.ReverseAdd(value);

    public static RuleSet operator +(bool value, RuleSet ruleSet) => ruleSet.ReverseAdd(value);

    public static RuleSet operator +(char value, RuleSet ruleSet) => ruleSet.ReverseAdd(value);

    public static RuleSet operator -(RuleSet left, RuleSet right)
    {
        return left.Subtract(right);
    }

    public static RuleSet operator -(RuleSet ruleSet, double value)
    {
        return ruleSet.Subtract(value);
    }

    public static RuleSet operator -(RuleSet ruleSet, int value) => ruleSet.Subtract(value);

    public static RuleSet operator -(RuleSet ruleSet, uint value) => ruleSet.Subtract(value);

    public static RuleSet operator -(RuleSet ruleSet, long value) => ruleSet.Subtract(value);

    public static RuleSet operator -(RuleSet ruleSet, ulong value) => ruleSet.Subtract(value);

    public static RuleSet operator -(RuleSet ruleSet, BigInteger value) => ruleSet.Subtract(value);

    public static RuleSet operator -(RuleSet ruleSet, bool value) => ruleSet.Subtract(value);

    public static RuleSet operator -(RuleSet ruleSet, char value) => ruleSet.Subtract(value);

    public static RuleSet operator -(double value, RuleSet ruleSet)
    {
        return ruleSet.ReverseSubtract(value);
    }

    public static RuleSet operator -(int value, RuleSet ruleSet) => ruleSet.ReverseSubtract(value);

    public static RuleSet operator -(uint value, RuleSet ruleSet) => ruleSet.ReverseSubtract(value);

    public static RuleSet operator -(long value, RuleSet ruleSet) => ruleSet.ReverseSubtract(value);

    public static RuleSet operator -(ulong value, RuleSet ruleSet) => ruleSet.ReverseSubtract(value);

    public static RuleSet operator -(BigInteger value, RuleSet ruleSet) => ruleSet.ReverseSubtract(value);

    public static RuleSet operator -(bool value, RuleSet ruleSet) => ruleSet.ReverseSubtract(value);

    public static RuleSet operator -(char value, RuleSet ruleSet) => ruleSet.ReverseSubtract(value);

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

    private static string NormalizeName(string? name)
    {
        return name is null
            ? "anonymous"
            : DomainGuard.RequiredText(name, nameof(name));
    }

    private static ScheduleType ValidateScheduleType(ScheduleType type, string parameterName)
    {
        if (!Enum.IsDefined(typeof(ScheduleType), type))
        {
            throw new ArgumentOutOfRangeException(parameterName, type, "Unknown schedule type.");
        }

        return type;
    }

    private static DaySchedule CoerceDaySchedule(
        object value,
        ScheduleType type,
        string name,
        string parameterName)
    {
        if (value is DaySchedule daySchedule)
        {
            if (daySchedule.Type != type)
            {
                throw new ArgumentException(
                    $"DaySchedule type mismatch: expected {type.CanonicalName()}, got {daySchedule.Type.CanonicalName()}.",
                    parameterName);
            }

            return daySchedule;
        }

        try
        {
            return DaySchedule.ConstantFromPythonScalar(name, value, type);
        }
        catch (ScheduleOperationException exception)
        {
            throw new ArgumentException(
                "A rule-set day value must be a Python-compatible bool, integer, float, or DaySchedule.",
                parameterName,
                exception);
        }
    }

    private static DaySchedule? CoerceOptionalDaySchedule(
        object? value,
        ScheduleType type,
        string name,
        string parameterName)
    {
        return value is null
            ? null
            : CoerceDaySchedule(value, type, name, parameterName);
    }

    private DaySchedule? GetExplicitDay(string key)
    {
        return key switch
        {
            "monday" => Monday,
            "tuesday" => Tuesday,
            "wednesday" => Wednesday,
            "thursday" => Thursday,
            "friday" => Friday,
            "saturday" => Saturday,
            "sunday" => Sunday,
            "holiday" => Holiday,
            _ => throw UnknownDayKey(key, nameof(key)),
        };
    }

    private static bool IsWeekdayKey(string key)
    {
        return key == "monday"
            || key == "tuesday"
            || key == "wednesday"
            || key == "thursday"
            || key == "friday";
    }

    private static ArgumentException UnknownDayKey(string key, string parameterName)
    {
        return new ArgumentException($"Unknown RuleSet day key: {PythonRepr(key)}.", parameterName);
    }

    private RuleSet Reconstruct(
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
        bool replaceMonday = false,
        bool replaceTuesday = false,
        bool replaceWednesday = false,
        bool replaceThursday = false,
        bool replaceFriday = false,
        bool replaceSaturday = false,
        bool replaceSunday = false,
        bool replaceHoliday = false)
    {
        return new RuleSet(
            Name,
            weekdays ?? Weekdays,
            weekends ?? Weekends,
            replaceMonday ? monday : Monday,
            replaceTuesday ? tuesday : Tuesday,
            replaceWednesday ? wednesday : Wednesday,
            replaceThursday ? thursday : Thursday,
            replaceFriday ? friday : Friday,
            replaceSaturday ? saturday : Saturday,
            replaceSunday ? sunday : Sunday,
            replaceHoliday ? holiday : Holiday,
            Type);
    }

    private static void AppendDayIdfFields(
        List<string> fields,
        string selection,
        DaySchedule daySchedule)
    {
        fields.Add($"For: {selection}");
        fields.AddRange(daySchedule.ToIdfCompactExpression());
    }

    private static string FormatPythonGeneral(double value)
    {
        const int precision = 4;
        long signedBits = BitConverter.DoubleToInt64Bits(value);
        bool isNegative = signedBits < 0;
        ulong bits = unchecked((ulong)signedBits);
        ulong magnitudeBits = bits & 0x7fff_ffff_ffff_ffffUL;
        if (magnitudeBits == 0)
        {
            return isNegative ? "-0" : "0";
        }

        int exponentBits = (int)((magnitudeBits >> 52) & 0x7ffUL);
        if (exponentBits == 0x7ff)
        {
            if ((magnitudeBits & 0x000f_ffff_ffff_ffffUL) != 0)
            {
                return "nan";
            }

            return isNegative ? "-inf" : "inf";
        }

        ulong fractionBits = magnitudeBits & 0x000f_ffff_ffff_ffffUL;
        ulong significand = exponentBits == 0
            ? fractionBits
            : fractionBits | 0x0010_0000_0000_0000UL;
        int binaryExponent = exponentBits == 0
            ? -1074
            : exponentBits - 1023 - 52;
        BigInteger numerator = new(significand);
        BigInteger denominator = BigInteger.One;
        if (binaryExponent >= 0)
        {
            numerator <<= binaryExponent;
        }
        else
        {
            denominator <<= -binaryExponent;
        }

        int decimalExponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        while (CompareRationalToPowerOfTen(numerator, denominator, decimalExponent) < 0)
        {
            decimalExponent--;
        }

        while (CompareRationalToPowerOfTen(numerator, denominator, decimalExponent + 1) >= 0)
        {
            decimalExponent++;
        }

        int decimalScale = precision - 1 - decimalExponent;
        BigInteger scaledNumerator = numerator;
        BigInteger scaledDenominator = denominator;
        if (decimalScale >= 0)
        {
            scaledNumerator *= BigInteger.Pow(10, decimalScale);
        }
        else
        {
            scaledDenominator *= BigInteger.Pow(10, -decimalScale);
        }

        BigInteger rounded = BigInteger.DivRem(
            scaledNumerator,
            scaledDenominator,
            out BigInteger remainder);
        int midpointComparison = (remainder << 1).CompareTo(scaledDenominator);
        if (midpointComparison > 0 || (midpointComparison == 0 && !rounded.IsEven))
        {
            rounded += BigInteger.One;
        }

        BigInteger overflowThreshold = BigInteger.Pow(10, precision);
        if (rounded == overflowThreshold)
        {
            rounded /= 10;
            decimalExponent++;
        }

        string digits = rounded.ToString(CultureInfo.InvariantCulture);
        string formatted;
        if (decimalExponent < -4 || decimalExponent >= precision)
        {
            string fractionalDigits = digits.Remove(0, 1).TrimEnd('0');
            string mantissa = fractionalDigits.Length == 0
                ? digits[0].ToString()
                : $"{digits[0]}.{fractionalDigits}";
            string exponentSign = decimalExponent >= 0 ? "+" : "-";
            formatted = $"{mantissa}e{exponentSign}{Math.Abs(decimalExponent):D2}";
        }
        else
        {
            int decimalPosition = decimalExponent + 1;
            if (decimalPosition <= 0)
            {
                formatted = $"0.{new string('0', -decimalPosition)}{digits}";
            }
            else if (decimalPosition >= digits.Length)
            {
                formatted = digits + new string('0', decimalPosition - digits.Length);
            }
            else
            {
                formatted = digits.Insert(decimalPosition, ".");
            }

            if (ContainsCharacter(formatted, '.'))
            {
                formatted = formatted.TrimEnd('0').TrimEnd('.');
            }
        }

        return isNegative ? $"-{formatted}" : formatted;
    }

    private static int CompareRationalToPowerOfTen(
        BigInteger numerator,
        BigInteger denominator,
        int exponent)
    {
        return exponent >= 0
            ? numerator.CompareTo(denominator * BigInteger.Pow(10, exponent))
            : (numerator * BigInteger.Pow(10, -exponent)).CompareTo(denominator);
    }

    private static string PythonRepr(string value)
    {
        char quote = ContainsCharacter(value, '\'')
            && !ContainsCharacter(value, '"')
                ? '"'
                : '\'';
        var result = new StringBuilder(value.Length + 2);
        result.Append(quote);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            switch (character)
            {
                case '\\': result.Append("\\\\"); break;
                case '\n': result.Append("\\n"); break;
                case '\r': result.Append("\\r"); break;
                case '\t': result.Append("\\t"); break;
                case '\b': result.Append("\\x08"); break;
                case '\f': result.Append("\\x0c"); break;
                default:
                    if (character == quote)
                    {
                        result.Append('\\').Append(character);
                    }
                    else if (char.IsHighSurrogate(character)
                        && index + 1 < value.Length
                        && char.IsLowSurrogate(value[index + 1]))
                    {
                        int codePoint = char.ConvertToUtf32(character, value[index + 1]);
                        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(value, index);
                        if (IsPythonPrintable(codePoint, category))
                        {
                            result.Append(character).Append(value[index + 1]);
                        }
                        else
                        {
                            AppendPythonUnicodeEscape(result, codePoint);
                        }

                        index++;
                    }
                    else
                    {
                        UnicodeCategory category = char.GetUnicodeCategory(character);
                        if (IsPythonPrintable(character, category))
                        {
                            result.Append(character);
                        }
                        else
                        {
                            AppendPythonUnicodeEscape(result, character);
                        }
                    }

                    break;
            }
        }

        return result.Append(quote).ToString();
    }

    private static bool IsPythonPrintable(int codePoint, UnicodeCategory category)
    {
        if (codePoint == 0x20)
        {
            return true;
        }

        return category is not UnicodeCategory.Control
            and not UnicodeCategory.Format
            and not UnicodeCategory.Surrogate
            and not UnicodeCategory.PrivateUse
            and not UnicodeCategory.OtherNotAssigned
            and not UnicodeCategory.SpaceSeparator
            and not UnicodeCategory.LineSeparator
            and not UnicodeCategory.ParagraphSeparator;
    }

    private static void AppendPythonUnicodeEscape(StringBuilder result, int codePoint)
    {
        if (codePoint <= byte.MaxValue)
        {
            result.Append("\\x")
                .Append(codePoint.ToString("x2", CultureInfo.InvariantCulture));
        }
        else if (codePoint <= char.MaxValue)
        {
            result.Append("\\u")
                .Append(codePoint.ToString("x4", CultureInfo.InvariantCulture));
        }
        else
        {
            result.Append("\\U")
                .Append(codePoint.ToString("x8", CultureInfo.InvariantCulture));
        }
    }

    private static bool ContainsCharacter(string value, char target)
    {
        foreach (char character in value)
        {
            if (character == target)
            {
                return true;
            }
        }

        return false;
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

    private RuleSet MultiplyPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "multiplication");
        return Map(
            day => day.MultiplyPythonScalar(value),
            $"{Name}:MUL:{scalarName}");
    }

    private RuleSet AddPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "addition");
        return Map(
            day => day.AddPythonScalar(value),
            $"{Name}:ADD:{scalarName}");
    }

    private RuleSet ReverseAddPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "reverse addition");
        return Map(
            day => day.AddPythonScalar(value),
            $"{scalarName}:ADD:{Name}");
    }

    private RuleSet SubtractPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "subtraction");
        return Map(
            day => day.SubtractPythonScalar(value),
            $"{Name}:SUB:{scalarName}");
    }

    private RuleSet ReverseSubtractPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "reverse subtraction");
        return Map(
            day => day.ReverseSubtractPythonScalar(value),
            $"{scalarName}:SUB:{Name}");
    }

    private RuleSet DividePythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "division");
        return Map(
            day => day.DividePythonScalar(value),
            $"{Name}:DIV:{scalarName}");
    }

    private RuleSet ReverseDividePythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "reverse division");
        return Map(
            day => day.ReverseDividePythonScalar(value),
            $"{scalarName}:DIV:{Name}");
    }

    private RuleSet ElementEqualPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "equality comparison");
        return Map(
            day => day.ElementEqual(value),
            $"{Name}:EQ:{scalarName}");
    }

    private RuleSet ElementNotEqualPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "inequality comparison");
        return Map(
            day => day.ElementNotEqual(value),
            $"{Name}:NE:{scalarName}");
    }

    private RuleSet LessThanPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "less-than comparison");
        return Map(
            day => day.LessThan(value),
            $"{Name}:LT:{scalarName}");
    }

    private RuleSet LessThanOrEqualPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "less-than-or-equal comparison");
        return Map(
            day => day.LessThanOrEqual(value),
            $"{Name}:LE:{scalarName}");
    }

    private RuleSet GreaterThanPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "greater-than comparison");
        return Map(
            day => day.GreaterThan(value),
            $"{Name}:GT:{scalarName}");
    }

    private RuleSet GreaterThanOrEqualPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "greater-than-or-equal comparison");
        return Map(
            day => day.GreaterThanOrEqual(value),
            $"{Name}:GE:{scalarName}");
    }

    private RuleSet ElementMinimumPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "element-wise minimum");
        return Map(
            day => day.ElementMinimum(value),
            $"{Name}:MIN:{scalarName}");
    }

    private RuleSet ElementMaximumPythonScalar<T>(T value)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "element-wise maximum");
        return Map(
            day => day.ElementMaximum(value),
            $"{Name}:MAX:{scalarName}");
    }

    private static WhereOperand CoerceWhereValue(
        object value,
        ScheduleType? type,
        string parameterName)
    {
        if (value is RuleSet ruleSet)
        {
            return WhereOperand.FromRuleSet(ruleSet);
        }

        if (value is DaySchedule daySchedule)
        {
            return WhereOperand.FromDaySchedule(daySchedule);
        }

        ScheduleType resultType = type ?? ScheduleType.Real;
        return value switch
        {
            bool scalar => WhereOperand.FromScalar(scalar, resultType),
            sbyte scalar => WhereOperand.FromScalar(scalar, resultType),
            byte scalar => WhereOperand.FromScalar(scalar, resultType),
            short scalar => WhereOperand.FromScalar(scalar, resultType),
            ushort scalar => WhereOperand.FromScalar(scalar, resultType),
            int scalar => WhereOperand.FromScalar(scalar, resultType),
            uint scalar => WhereOperand.FromScalar(scalar, resultType),
            long scalar => WhereOperand.FromScalar(scalar, resultType),
            ulong scalar => WhereOperand.FromScalar(scalar, resultType),
            BigInteger scalar => WhereOperand.FromScalar(scalar, resultType),
            float scalar => WhereOperand.FromScalar(scalar, resultType),
            double scalar => WhereOperand.FromScalar(scalar, resultType),
            _ => throw new ArgumentException(
                "A conditional value must be a Python-compatible bool, integer, float, DaySchedule, or RuleSet.",
                parameterName),
        };
    }

    private static DaySchedule SelectWhereDay(
        DaySchedule condition,
        object whenTrue,
        object whenFalse,
        string name,
        ScheduleType? type)
    {
        if (whenTrue is DaySchedule trueSchedule)
        {
            return whenFalse is DaySchedule falseSchedule
                ? DaySchedule.Where(condition, trueSchedule, falseSchedule, name, type)
                : DaySchedule.Where(condition, trueSchedule, (double)whenFalse, name, type);
        }

        return whenFalse is DaySchedule falseDaySchedule
            ? DaySchedule.Where(condition, (double)whenTrue, falseDaySchedule, name, type)
            : DaySchedule.Where(condition, (double)whenTrue, (double)whenFalse, name, type);
    }

    private static DaySchedule? WhereOverride(
        DaySchedule? conditionOverride,
        DaySchedule? trueOverride,
        DaySchedule? falseOverride,
        DaySchedule conditionFallback,
        object trueFallback,
        object falseFallback,
        string name,
        ScheduleType? type)
    {
        if (conditionOverride is null && trueOverride is null && falseOverride is null)
        {
            return null;
        }

        return SelectWhereDay(
            conditionOverride ?? conditionFallback,
            trueOverride ?? trueFallback,
            falseOverride ?? falseFallback,
            name,
            type);
    }

    private sealed class WhereOperand
    {
        private WhereOperand(
            object weekdays,
            object weekends,
            ScheduleType type,
            DaySchedule? monday = null,
            DaySchedule? tuesday = null,
            DaySchedule? wednesday = null,
            DaySchedule? thursday = null,
            DaySchedule? friday = null,
            DaySchedule? saturday = null,
            DaySchedule? sunday = null,
            DaySchedule? holiday = null)
        {
            Weekdays = weekdays;
            Weekends = weekends;
            Type = type;
            Monday = monday;
            Tuesday = tuesday;
            Wednesday = wednesday;
            Thursday = thursday;
            Friday = friday;
            Saturday = saturday;
            Sunday = sunday;
            Holiday = holiday;
        }

        public object Weekdays { get; }

        public object Weekends { get; }

        public ScheduleType Type { get; }

        public DaySchedule? Monday { get; }

        public DaySchedule? Tuesday { get; }

        public DaySchedule? Wednesday { get; }

        public DaySchedule? Thursday { get; }

        public DaySchedule? Friday { get; }

        public DaySchedule? Saturday { get; }

        public DaySchedule? Sunday { get; }

        public DaySchedule? Holiday { get; }

        public static WhereOperand FromRuleSet(RuleSet value)
        {
            return new WhereOperand(
                value.Weekdays,
                value.Weekends,
                value.Type,
                value.Monday,
                value.Tuesday,
                value.Wednesday,
                value.Thursday,
                value.Friday,
                value.Saturday,
                value.Sunday,
                value.Holiday);
        }

        public static WhereOperand FromDaySchedule(DaySchedule value)
        {
            return new WhereOperand(value, value, value.Type);
        }

        public static WhereOperand FromScalar<T>(T value, ScheduleType type)
        {
            double scalar = DaySchedule.ConvertPythonScalarToScheduleValue(
                value,
                type,
                "conditional value");
            return new WhereOperand(scalar, scalar, type);
        }
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
