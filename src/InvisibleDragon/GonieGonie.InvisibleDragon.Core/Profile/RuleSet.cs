using System.Numerics;
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
