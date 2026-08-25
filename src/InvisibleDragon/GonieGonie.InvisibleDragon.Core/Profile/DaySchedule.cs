using System.Collections;
using System.Collections.ObjectModel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Profile;

public sealed record DayScheduleSegment
{
    public DayScheduleSegment(TimeSpan until, double value)
    {
        if (until <= TimeSpan.Zero || until > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(until), until, "Until time must be in (00:00, 24:00].");
        }

        Until = until;
        Value = DomainGuard.Finite(value, nameof(value));
    }

    public TimeSpan Until { get; }

    public double Value { get; }
}

public sealed record DayScheduleWindow
{
    public DayScheduleWindow(TimeSpan start, TimeSpan end, double value)
    {
        if (start < TimeSpan.Zero || start >= TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Start time must be in [00:00, 24:00).");
        }

        if (end <= start || end > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(end), end, "End time must be after start and no later than 24:00.");
        }

        Start = start;
        End = end;
        Value = DomainGuard.Finite(value, nameof(value));
    }

    public TimeSpan Start { get; }

    public TimeSpan End { get; }

    public double Value { get; }
}

/// <summary>
/// An immutable 24-hour schedule sampled at the upstream ten-minute interval.
/// </summary>
public sealed class DaySchedule : IReadOnlyList<double>, IEquatable<DaySchedule>
{
    public const int IntervalsPerHour = 6;

    public const int FixedLength = 24 * IntervalsPerHour;

    public static readonly TimeSpan Step = TimeSpan.FromMinutes(60d / IntervalsPerHour);

    public DaySchedule(
        string name,
        IEnumerable<double> values,
        ScheduleType type = ScheduleType.Real,
        string? unit = null)
    {
        Name = DomainGuard.RequiredText(name, nameof(name));
        DomainGuard.NotNull(values, nameof(values));
        if (!Enum.IsDefined(typeof(ScheduleType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown schedule type.");
        }

        double[] copy = values.ToArray();
        if (copy.Length != FixedLength)
        {
            throw new ArgumentException($"A day schedule requires exactly {FixedLength} values.", nameof(values));
        }

        for (int index = 0; index < copy.Length; index++)
        {
            copy[index] = type.ValidateValue(copy[index], nameof(values));
        }

        Type = type;
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit!.Trim();
        Values = new ReadOnlyCollection<double>(copy);
    }

    public string Name { get; }

    public ScheduleType Type { get; }

    public string? Unit { get; }

    public IReadOnlyList<double> Values { get; }

    public int Count => FixedLength;

    public double this[int index] => Values[index];

    public double Minimum => Values.Min();

    public double Maximum => Values.Max();

    public double IntegralHours => Values.Sum() / IntervalsPerHour;

    public double Average => Values.Average();

    public double PositiveHours => Values.Count(value => value > 0) / (double)IntervalsPerHour;

    public double NonzeroHours => Values.Count(value => value != 0) / (double)IntervalsPerHour;

    public bool HasPositive => Values.Any(value => value > 0);

    public bool HasNonzero => Values.Any(value => value != 0);

    public double PositiveAverage
    {
        get
        {
            double[] positiveValues = Values.Where(value => value > 0).ToArray();
            return positiveValues.Length == 0 ? 0 : positiveValues.Average();
        }
    }

    public bool IsConstant => Values.All(value => value.Equals(Values[0]));

    public static ValidationResult Validate(IEnumerable<double> values, ScheduleType type)
    {
        DomainGuard.NotNull(values, nameof(values));
        double[] copy = values.ToArray();
        List<Diagnostic> diagnostics = new();
        if (copy.Length != FixedLength)
        {
            diagnostics.Add(new Diagnostic(
                "INVISIBLEDRAGON.SCHEDULE.INVALID_DAY_LENGTH",
                DiagnosticSeverity.Error,
                $"A day schedule requires exactly {FixedLength} values; {copy.Length} were supplied.",
                suggestedAction: "Resample the input to ten-minute intervals."));
        }

        for (int index = 0; index < copy.Length; index++)
        {
            try
            {
                type.ValidateValue(copy[index]);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                diagnostics.Add(new Diagnostic(
                    "INVISIBLEDRAGON.SCHEDULE.VALUE_OUT_OF_RANGE",
                    DiagnosticSeverity.Error,
                    $"Schedule value at index {index} is invalid for {type}: {exception.Message}",
                    suggestedAction: "Correct the value or choose the matching schedule type."));
            }
        }

        return diagnostics.Count == 0 ? ValidationResult.Success : ValidationResult.From(diagnostics);
    }

    public static DaySchedule Constant(
        string name,
        double value,
        ScheduleType type = ScheduleType.Real,
        string? unit = null)
    {
        return new DaySchedule(name, Enumerable.Repeat(value, FixedLength), type, unit);
    }

    public static DaySchedule FromCompact(
        string name,
        IEnumerable<DayScheduleSegment> segments,
        ScheduleType type = ScheduleType.Real,
        string? unit = null)
    {
        DomainGuard.NotNull(segments, nameof(segments));
        DayScheduleSegment[] copy = segments.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("At least one compact segment is required.", nameof(segments));
        }

        TimeSpan previous = TimeSpan.Zero;
        foreach (DayScheduleSegment segment in copy)
        {
            if (segment is null)
            {
                throw new ArgumentException("Compact segments cannot contain null.", nameof(segments));
            }

            if (segment.Until <= previous)
            {
                throw new ArgumentException("Compact segment end times must increase strictly.", nameof(segments));
            }

            if (segment.Until.Ticks % Step.Ticks != 0)
            {
                throw new ArgumentException("Compact segment end times must align to ten-minute intervals.", nameof(segments));
            }

            type.ValidateValue(segment.Value, nameof(segments));
            previous = segment.Until;
        }

        if (copy[copy.Length - 1].Until != TimeSpan.FromHours(24))
        {
            throw new ArgumentException("The final compact segment must end at 24:00.", nameof(segments));
        }

        double[] values = new double[FixedLength];
        int segmentIndex = 0;
        for (int index = 0; index < values.Length; index++)
        {
            TimeSpan intervalEnd = TimeSpan.FromTicks(Step.Ticks * (index + 1L));
            while (intervalEnd > copy[segmentIndex].Until)
            {
                segmentIndex++;
            }

            values[index] = copy[segmentIndex].Value;
        }

        return new DaySchedule(name, values, type, unit);
    }

    public static DaySchedule FromWindows(
        string name,
        double defaultValue,
        IEnumerable<DayScheduleWindow> windows,
        ScheduleType type = ScheduleType.Real,
        string? unit = null)
    {
        DomainGuard.NotNull(windows, nameof(windows));
        DayScheduleWindow[] copy = windows.ToArray();
        if (copy.Any(window => window is null))
        {
            throw new ArgumentException("Schedule windows cannot contain null.", nameof(windows));
        }

        type.ValidateValue(defaultValue, nameof(defaultValue));
        foreach (DayScheduleWindow window in copy)
        {
            type.ValidateValue(window.Value, nameof(windows));
        }

        double[] values = new double[FixedLength];
        for (int index = 0; index < values.Length; index++)
        {
            TimeSpan intervalStart = TimeSpan.FromTicks(Step.Ticks * index);
            DayScheduleWindow? match = copy.FirstOrDefault(
                window => window.Start <= intervalStart && intervalStart < window.End);
            values[index] = match?.Value ?? defaultValue;
        }

        return new DaySchedule(name, values, type, unit);
    }

    public DaySchedule WithValue(int index, double value)
    {
        double[] copy = Values.ToArray();
        copy[index] = Type.ValidateValue(value);
        return new DaySchedule(Name, copy, Type, Unit);
    }

    public DaySchedule AsType(ScheduleType type)
    {
        return new DaySchedule(Name, Values, type, Unit);
    }

    public DaySchedule Clip(double? minimum = null, double? maximum = null, string? name = null)
    {
        if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
        {
            throw new ArgumentException("Minimum cannot exceed maximum.");
        }

        return new DaySchedule(
            name ?? $"{Name}:CLIP",
            Values.Select(value => Math.Min(maximum ?? double.PositiveInfinity, Math.Max(minimum ?? double.NegativeInfinity, value))),
            Type,
            Unit);
    }

    public DaySchedule NormalizeByMaximum(string? name = null)
    {
        double divisor = Maximum == 0 ? 1 : Maximum;
        return new DaySchedule(
            name ?? $"{Name}_normalized",
            Values.Select(value => value / divisor),
            Type,
            Unit);
    }

    public IReadOnlyList<DayScheduleSegment> Compactize()
    {
        List<DayScheduleSegment> segments = new();
        for (int index = 0; index < Values.Count; index++)
        {
            TimeSpan until = TimeSpan.FromTicks(Step.Ticks * (index + 1L));
            if (index == 0 || Values[index] != Values[index - 1])
            {
                segments.Add(new DayScheduleSegment(until, Values[index]));
            }
            else
            {
                segments[segments.Count - 1] = new DayScheduleSegment(until, Values[index]);
            }
        }

        return new ReadOnlyCollection<DayScheduleSegment>(segments);
    }

    public DaySchedule ElementEqual(double value)
    {
        return Compare(value, (left, right) => left == right, "EQ");
    }

    public DaySchedule ElementEqual(DaySchedule other)
    {
        return Compare(other, (left, right) => left == right, "EQ");
    }

    public DaySchedule ElementNotEqual(double value)
    {
        return Compare(value, (left, right) => left != right, "NE");
    }

    public DaySchedule ElementNotEqual(DaySchedule other)
    {
        return Compare(other, (left, right) => left != right, "NE");
    }

    public DaySchedule LessThan(double value)
    {
        return Compare(value, (left, right) => left < right, "LT");
    }

    public DaySchedule LessThan(DaySchedule other)
    {
        return Compare(other, (left, right) => left < right, "LT");
    }

    public DaySchedule LessThanOrEqual(double value)
    {
        return Compare(value, (left, right) => left <= right, "LE");
    }

    public DaySchedule LessThanOrEqual(DaySchedule other)
    {
        return Compare(other, (left, right) => left <= right, "LE");
    }

    public DaySchedule GreaterThan(double value)
    {
        return Compare(value, (left, right) => left > right, "GT");
    }

    public DaySchedule GreaterThan(DaySchedule other)
    {
        return Compare(other, (left, right) => left > right, "GT");
    }

    public DaySchedule GreaterThanOrEqual(double value)
    {
        return Compare(value, (left, right) => left >= right, "GE");
    }

    public DaySchedule GreaterThanOrEqual(DaySchedule other)
    {
        return Compare(other, (left, right) => left >= right, "GE");
    }

    public DaySchedule ElementMinimum(DaySchedule other)
    {
        RequireSameNonOnOffType(other, "minimum");
        return new DaySchedule(
            $"{Name}:MIN:{other.Name}",
            Enumerable.Range(0, FixedLength).Select(index => Math.Min(Values[index], other.Values[index])),
            Type);
    }

    public DaySchedule ElementMinimum(double other)
    {
        RequireElementExtremaType("minimum");
        return new DaySchedule(
            $"{Name}:MIN:{other}",
            Values.Select(value => Math.Min(value, other)),
            Type);
    }

    public DaySchedule ElementMaximum(DaySchedule other)
    {
        RequireSameNonOnOffType(other, "maximum");
        return new DaySchedule(
            $"{Name}:MAX:{other.Name}",
            Enumerable.Range(0, FixedLength).Select(index => Math.Max(Values[index], other.Values[index])),
            Type);
    }

    public DaySchedule ElementMaximum(double other)
    {
        RequireElementExtremaType("maximum");
        return new DaySchedule(
            $"{Name}:MAX:{other}",
            Values.Select(value => Math.Max(value, other)),
            Type);
    }

    public DaySchedule IsOn()
    {
        return ElementEqual(1);
    }

    public DaySchedule IsOff()
    {
        return ElementEqual(0);
    }

    public DaySchedule IsPositive()
    {
        return GreaterThan(0);
    }

    public DaySchedule IsNegative()
    {
        return LessThan(0);
    }

    public DaySchedule IsZero()
    {
        return ElementEqual(0);
    }

    public DaySchedule IsNonzero()
    {
        return ElementNotEqual(0);
    }

    public DaySchedule IsBetween(
        double minimum,
        double maximum,
        bool includeMinimum = true,
        bool includeMaximum = true)
    {
        DaySchedule lower = includeMinimum ? GreaterThanOrEqual(minimum) : GreaterThan(minimum);
        DaySchedule upper = includeMaximum ? LessThanOrEqual(maximum) : LessThan(maximum);
        return lower & upper;
    }

    public static DaySchedule Where(
        DaySchedule condition,
        DaySchedule whenTrue,
        DaySchedule whenFalse,
        string? name = null,
        ScheduleType? type = null)
    {
        ScheduleType resultType = ResolveWhereType(condition, type, whenTrue, whenFalse);
        return CreateWhere(
            condition,
            index => whenTrue[index],
            index => whenFalse[index],
            resultType,
            name);
    }

    public static DaySchedule Where(
        DaySchedule condition,
        DaySchedule whenTrue,
        double whenFalse,
        string? name = null,
        ScheduleType? type = null)
    {
        ScheduleType resultType = ResolveWhereType(condition, type, whenTrue);
        return CreateWhere(
            condition,
            index => whenTrue[index],
            _ => whenFalse,
            resultType,
            name);
    }

    public static DaySchedule Where(
        DaySchedule condition,
        double whenTrue,
        DaySchedule whenFalse,
        string? name = null,
        ScheduleType? type = null)
    {
        ScheduleType resultType = ResolveWhereType(condition, type, whenFalse);
        return CreateWhere(
            condition,
            _ => whenTrue,
            index => whenFalse[index],
            resultType,
            name);
    }

    public static DaySchedule Where(
        DaySchedule condition,
        double whenTrue,
        double whenFalse,
        string? name = null,
        ScheduleType? type = null)
    {
        ScheduleType resultType = ResolveWhereType(condition, type);
        return CreateWhere(
            condition,
            _ => whenTrue,
            _ => whenFalse,
            resultType,
            name);
    }

    public static DaySchedule operator *(DaySchedule left, DaySchedule right)
    {
        ScheduleType resultType = MultiplicationType(left.Type, right.Type);
        return left.Zip(right, (a, b) => a * b, resultType, "MUL");
    }

    public static DaySchedule operator *(DaySchedule schedule, double factor)
    {
        ScheduleType resultType = schedule.Type == ScheduleType.OnOff ? ScheduleType.Real : schedule.Type;
        return schedule.Map(value => value * factor, resultType, schedule.Name);
    }

    public static DaySchedule operator *(double factor, DaySchedule schedule)
    {
        return schedule * factor;
    }

    public static DaySchedule operator /(DaySchedule left, DaySchedule right)
    {
        if (left.Type == ScheduleType.OnOff || right.Type != ScheduleType.Real)
        {
            throw new ScheduleOperationException("Schedule division requires a non-OnOff numerator and a Real denominator.");
        }

        return left.Zip(right, Divide, left.Type, "DIV");
    }

    public static DaySchedule operator /(DaySchedule schedule, double divisor)
    {
        ScheduleType resultType = schedule.Type == ScheduleType.OnOff
            ? ScheduleType.Real
            : schedule.Type;
        return schedule.Map(value => Divide(value, divisor), resultType, schedule.Name);
    }

    public static DaySchedule operator /(double numerator, DaySchedule denominator)
    {
        DomainGuard.NotNull(denominator, nameof(denominator));
        if (denominator.Type != ScheduleType.Real)
        {
            throw new ScheduleOperationException("A scalar can only be divided by a Real schedule.");
        }

        if (denominator.Values.Any(value => value == 0))
        {
            throw new DivideByZeroException("A scalar cannot be divided by a schedule containing zero.");
        }

        return new DaySchedule(
            denominator.Name,
            denominator.Values.Select(value => numerator / value),
            ScheduleType.Real);
    }

    public static DaySchedule operator +(DaySchedule left, DaySchedule right)
    {
        ScheduleType resultType = AdditionType(left.Type, right.Type);
        return left.Zip(right, (a, b) => a + b, resultType, "ADD");
    }

    public static DaySchedule operator +(DaySchedule schedule, double value)
    {
        RequireScalarArithmetic(schedule.Type, "addition");
        return schedule.Map(item => item + value, schedule.Type, $"{schedule.Name}:ADD:{value}");
    }

    public static DaySchedule operator +(double value, DaySchedule schedule)
    {
        return schedule + value;
    }

    public static DaySchedule operator -(DaySchedule left, DaySchedule right)
    {
        ScheduleType resultType = AdditionType(left.Type, right.Type);
        return left.Zip(right, (a, b) => a - b, resultType, "SUB");
    }

    public static DaySchedule operator -(DaySchedule schedule, double value)
    {
        RequireScalarArithmetic(schedule.Type, "subtraction");
        return schedule.Map(item => item - value, schedule.Type, $"{schedule.Name}:SUB:{value}");
    }

    public static DaySchedule operator -(double value, DaySchedule schedule)
    {
        DomainGuard.NotNull(schedule, nameof(schedule));
        RequireScalarArithmetic(schedule.Type, "reverse subtraction");
        return new DaySchedule(
            $"{schedule.Name}:SUB:{value}",
            schedule.Values.Select(item => value - item),
            schedule.Type);
    }

    public static DaySchedule operator &(DaySchedule left, DaySchedule right)
    {
        RequireOnOff(left, right, "AND");
        return left.Zip(right, (a, b) => a == 1 && b == 1 ? 1 : 0, ScheduleType.OnOff, "AND");
    }

    public static DaySchedule operator |(DaySchedule left, DaySchedule right)
    {
        RequireOnOff(left, right, "OR");
        return left.Zip(right, (a, b) => a == 1 || b == 1 ? 1 : 0, ScheduleType.OnOff, "OR");
    }

    public static DaySchedule operator !(DaySchedule schedule)
    {
        if (schedule.Type != ScheduleType.OnOff)
        {
            throw new ScheduleOperationException("Logical inversion requires an OnOff schedule.");
        }

        return schedule.Map(value => value == 1 ? 0 : 1, ScheduleType.OnOff, $"{schedule.Name}:INVERTED");
    }

    public bool Equals(DaySchedule? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(Name, other.Name)
            && Type == other.Type
            && StringComparer.Ordinal.Equals(Unit, other.Unit)
            && Values.SequenceEqual(other.Values);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as DaySchedule);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(Name);
            hash = (hash * 397) ^ Type.GetHashCode();
            hash = (hash * 397) ^ (Unit is null ? 0 : StringComparer.Ordinal.GetHashCode(Unit));
            foreach (double value in Values)
            {
                hash = (hash * 397) ^ value.GetHashCode();
            }

            return hash;
        }
    }

    public IEnumerator<double> GetEnumerator()
    {
        return Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private DaySchedule Compare(double value, Func<double, double, bool> comparison, string operation)
    {
        return new DaySchedule(
            $"{Name}:{operation}:{value}",
            Values.Select(item => comparison(item, value) ? 1d : 0d),
            ScheduleType.OnOff);
    }

    private DaySchedule Compare(
        DaySchedule other,
        Func<double, double, bool> comparison,
        string operation)
    {
        DomainGuard.NotNull(other, nameof(other));
        return new DaySchedule(
            $"{Name}:{operation}:{other.Name}",
            Enumerable.Range(0, FixedLength).Select(
                index => comparison(Values[index], other.Values[index]) ? 1d : 0d),
            ScheduleType.OnOff);
    }

    private static DaySchedule CreateWhere(
        DaySchedule condition,
        Func<int, double> whenTrue,
        Func<int, double> whenFalse,
        ScheduleType type,
        string? name)
    {
        return new DaySchedule(
            name ?? "WHERE",
            Enumerable.Range(0, FixedLength).Select(
                index => condition[index] == 1 ? whenTrue(index) : whenFalse(index)),
            type);
    }

    private static ScheduleType ResolveWhereType(
        DaySchedule condition,
        ScheduleType? requestedType,
        params DaySchedule[] resultSchedules)
    {
        DomainGuard.NotNull(condition, nameof(condition));
        if (condition.Type != ScheduleType.OnOff)
        {
            throw new ScheduleOperationException("The condition schedule must have OnOff type.");
        }

        if (requestedType.HasValue && !Enum.IsDefined(typeof(ScheduleType), requestedType.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedType), requestedType, "Unknown schedule type.");
        }

        foreach (DaySchedule schedule in resultSchedules)
        {
            DomainGuard.NotNull(schedule, nameof(resultSchedules));
        }

        ScheduleType resultType = requestedType
            ?? resultSchedules.FirstOrDefault()?.Type
            ?? ScheduleType.Real;
        if (resultSchedules.Any(schedule => schedule.Type != resultType))
        {
            throw new ScheduleOperationException("Conditional result schedules must have the same requested type.");
        }

        return resultType;
    }

    private DaySchedule Map(
        Func<double, double> operation,
        ScheduleType resultType,
        string resultName)
    {
        return new DaySchedule(resultName, Values.Select(operation), resultType, Unit);
    }

    private DaySchedule Zip(
        DaySchedule other,
        Func<double, double, double> operation,
        ScheduleType resultType,
        string operationName)
    {
        DomainGuard.NotNull(other, nameof(other));
        return new DaySchedule(
            $"{Name}:{operationName}:{other.Name}",
            Enumerable.Range(0, FixedLength).Select(index => operation(Values[index], other.Values[index])),
            resultType,
            Unit == other.Unit ? Unit : null);
    }

    private void RequireSameNonOnOffType(DaySchedule other, string operation)
    {
        DomainGuard.NotNull(other, nameof(other));
        if (Type == ScheduleType.OnOff || Type != other.Type)
        {
            throw new ScheduleOperationException($"Element-wise {operation} requires matching non-OnOff schedule types.");
        }
    }

    private void RequireElementExtremaType(string operation)
    {
        if (Type == ScheduleType.OnOff)
        {
            throw new ScheduleOperationException($"Element-wise {operation} is not defined for OnOff schedules.");
        }
    }

    private static void RequireOnOff(DaySchedule left, DaySchedule right, string operation)
    {
        if (left.Type != ScheduleType.OnOff || right.Type != ScheduleType.OnOff)
        {
            throw new ScheduleOperationException($"{operation} requires two OnOff schedules.");
        }
    }

    private static void RequireScalarArithmetic(ScheduleType type, string operation)
    {
        if (type == ScheduleType.OnOff)
        {
            throw new ScheduleOperationException($"Scalar {operation} is not defined for OnOff schedules.");
        }
    }

    private static ScheduleType MultiplicationType(ScheduleType left, ScheduleType right)
    {
        if (left == ScheduleType.Temperature || right == ScheduleType.Temperature)
        {
            return ScheduleType.Temperature;
        }

        if (left == ScheduleType.Real || right == ScheduleType.Real)
        {
            return ScheduleType.Real;
        }

        if (left == ScheduleType.Fraction || right == ScheduleType.Fraction)
        {
            return ScheduleType.Fraction;
        }

        return ScheduleType.OnOff;
    }

    private static ScheduleType AdditionType(ScheduleType left, ScheduleType right)
    {
        if (left == ScheduleType.OnOff || right == ScheduleType.OnOff)
        {
            throw new ScheduleOperationException("Addition and subtraction are not defined for OnOff schedules.");
        }

        if (left == ScheduleType.Fraction || right == ScheduleType.Fraction)
        {
            if (left != ScheduleType.Fraction || right != ScheduleType.Fraction)
            {
                throw new ScheduleOperationException("Fraction schedules can only be added to or subtracted from Fraction schedules.");
            }

            return ScheduleType.Fraction;
        }

        if (left == ScheduleType.Temperature || right == ScheduleType.Temperature)
        {
            return ScheduleType.Temperature;
        }

        return ScheduleType.Real;
    }

    private static double Divide(double numerator, double denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException("A schedule value cannot be divided by zero.");
        }

        return numerator / denominator;
    }
}
