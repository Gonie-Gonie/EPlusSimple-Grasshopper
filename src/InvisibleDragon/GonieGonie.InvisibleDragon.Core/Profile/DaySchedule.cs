using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
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

    public double IntegralHours => Python312FloatSum(Values) * Step.TotalHours;

    public double Average => Python312FloatSum(Values) / FixedLength;

    public double PositiveHours => Values.Count(value => value > 0) * Step.TotalHours;

    public double NonzeroHours => Values.Count(value => value != 0) * Step.TotalHours;

    public bool HasPositive => Values.Any(value => value > 0);

    public bool HasNonzero => Values.Any(value => value != 0);

    public double PositiveAverage
    {
        get
        {
            double[] positiveValues = Values.Where(value => value > 0).ToArray();
            return positiveValues.Length == 0
                ? 0
                : Python312FloatSum(positiveValues) / positiveValues.Length;
        }
    }

    public bool IsConstant => Values.All(value => value.Equals(Values[0]));

    // CPython 3.12.7 builtins.sum uses the improved Kahan-Babuska algorithm
    // by Arnold Neumaier for exact float inputs. DaySchedule.average,
    // integral, and positive_average all call that builtin upstream.
    private static double Python312FloatSum(IEnumerable<double> values)
    {
        double result = 0;
        double compensation = 0;
        foreach (double value in values)
        {
            double total = result + value;
            compensation += Math.Abs(result) >= Math.Abs(value)
                ? (result - total) + value
                : (value - total) + result;
            result = total;
        }

        if (compensation != 0
            && !double.IsNaN(compensation)
            && !double.IsInfinity(compensation))
        {
            result += compensation;
        }

        return result;
    }

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

    internal static string FormatPythonScalar<T>(T value, string operation)
    {
        return PythonScalar.Create(value, operation).Text;
    }

    internal static double ConvertPythonScalarToScheduleValue<T>(
        T value,
        ScheduleType type,
        string operation)
    {
        return PythonScalar.Create(value, operation).ToScheduleValue(type, operation);
    }

    internal static DaySchedule ConstantFromPythonScalar<T>(
        string name,
        T value,
        ScheduleType type)
    {
        PythonScalar scalar = PythonScalar.Create(value, "constant schedule value");
        double scheduleValue = scalar.ToScheduleValue(type, "constant schedule value");
        return new DaySchedule(
            name,
            Enumerable.Repeat(scheduleValue, FixedLength),
            type);
    }

    internal DaySchedule AddPythonScalar<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "addition");
        return AddScalar(this, scalar.ToPythonFloat("addition"), scalar.Text);
    }

    internal DaySchedule SubtractPythonScalar<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "subtraction");
        return SubtractScalar(this, scalar.ToPythonFloat("subtraction"), scalar.Text);
    }

    internal DaySchedule ReverseSubtractPythonScalar<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "reverse subtraction");
        return ReverseSubtractScalar(
            scalar.ToPythonFloat("reverse subtraction"),
            scalar.Text,
            this);
    }

    internal DaySchedule MultiplyPythonScalar<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "multiplication");
        return this * scalar.ToPythonFloat("multiplication");
    }

    internal DaySchedule DividePythonScalar<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "division");
        return this / scalar.ToPythonFloat("division");
    }

    internal DaySchedule ReverseDividePythonScalar<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "reverse division");
        return scalar.ToPythonFloat("reverse division") / this;
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
        ScheduleType resultType = Type == ScheduleType.OnOff
            ? ScheduleType.Real
            : Type;
        return new DaySchedule(
            name ?? $"{Name}_normalized",
            Values.Select(value => value / divisor),
            resultType);
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
        return Compare(
            PythonScalar.Create(value, "equality comparison"),
            PythonComparison.Equal,
            "EQ");
    }

    public DaySchedule ElementEqual<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "equality comparison");
        return Compare(scalar, PythonComparison.Equal, "EQ");
    }

    public DaySchedule ElementEqual(DaySchedule other)
    {
        return Compare(other, (left, right) => left == right, "EQ");
    }

    public DaySchedule ElementNotEqual(double value)
    {
        return Compare(
            PythonScalar.Create(value, "inequality comparison"),
            PythonComparison.NotEqual,
            "NE");
    }

    public DaySchedule ElementNotEqual<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "inequality comparison");
        return Compare(scalar, PythonComparison.NotEqual, "NE");
    }

    public DaySchedule ElementNotEqual(DaySchedule other)
    {
        return Compare(other, (left, right) => left != right, "NE");
    }

    public DaySchedule LessThan(double value)
    {
        return Compare(
            PythonScalar.Create(value, "less-than comparison"),
            PythonComparison.LessThan,
            "LT");
    }

    public DaySchedule LessThan<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "less-than comparison");
        return Compare(scalar, PythonComparison.LessThan, "LT");
    }

    public DaySchedule LessThan(DaySchedule other)
    {
        return Compare(other, (left, right) => left < right, "LT");
    }

    public DaySchedule LessThanOrEqual(double value)
    {
        return Compare(
            PythonScalar.Create(value, "less-than-or-equal comparison"),
            PythonComparison.LessThanOrEqual,
            "LE");
    }

    public DaySchedule LessThanOrEqual<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "less-than-or-equal comparison");
        return Compare(scalar, PythonComparison.LessThanOrEqual, "LE");
    }

    public DaySchedule LessThanOrEqual(DaySchedule other)
    {
        return Compare(other, (left, right) => left <= right, "LE");
    }

    public DaySchedule GreaterThan(double value)
    {
        return Compare(
            PythonScalar.Create(value, "greater-than comparison"),
            PythonComparison.GreaterThan,
            "GT");
    }

    public DaySchedule GreaterThan<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "greater-than comparison");
        return Compare(scalar, PythonComparison.GreaterThan, "GT");
    }

    public DaySchedule GreaterThan(DaySchedule other)
    {
        return Compare(other, (left, right) => left > right, "GT");
    }

    public DaySchedule GreaterThanOrEqual(double value)
    {
        return Compare(
            PythonScalar.Create(value, "greater-than-or-equal comparison"),
            PythonComparison.GreaterThanOrEqual,
            "GE");
    }

    public DaySchedule GreaterThanOrEqual<T>(T value)
    {
        PythonScalar scalar = PythonScalar.Create(value, "greater-than-or-equal comparison");
        return Compare(scalar, PythonComparison.GreaterThanOrEqual, "GE");
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
            Enumerable.Range(0, FixedLength).Select(index => PythonMinimum(Values[index], other.Values[index])),
            Type);
    }

    public DaySchedule ElementMinimum(double other)
    {
        return ElementMinimum(PythonScalar.Create(other, "element-wise minimum"));
    }

    public DaySchedule ElementMinimum<T>(T other)
    {
        PythonScalar scalar = PythonScalar.Create(other, "element-wise minimum");
        return ElementMinimum(scalar);
    }

    private DaySchedule ElementMinimum(PythonScalar other)
    {
        RequireElementExtremaType("minimum");
        return new DaySchedule(
            $"{Name}:MIN:{other.Text}",
            Values.Select(value => PythonMinimum(value, other, Type)),
            Type);
    }

    public DaySchedule ElementMaximum(DaySchedule other)
    {
        RequireSameNonOnOffType(other, "maximum");
        return new DaySchedule(
            $"{Name}:MAX:{other.Name}",
            Enumerable.Range(0, FixedLength).Select(index => PythonMaximum(Values[index], other.Values[index])),
            Type);
    }

    public DaySchedule ElementMaximum(double other)
    {
        return ElementMaximum(PythonScalar.Create(other, "element-wise maximum"));
    }

    public DaySchedule ElementMaximum<T>(T other)
    {
        PythonScalar scalar = PythonScalar.Create(other, "element-wise maximum");
        return ElementMaximum(scalar);
    }

    private DaySchedule ElementMaximum(PythonScalar other)
    {
        RequireElementExtremaType("maximum");
        return new DaySchedule(
            $"{Name}:MAX:{other.Text}",
            Values.Select(value => PythonMaximum(value, other, Type)),
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
        return IsBetween(
            PythonScalar.Create(minimum, "range minimum"),
            PythonScalar.Create(maximum, "range maximum"),
            includeMinimum,
            includeMaximum);
    }

    public DaySchedule IsBetween<TMinimum, TMaximum>(
        TMinimum minimum,
        TMaximum maximum,
        bool includeMinimum = true,
        bool includeMaximum = true)
    {
        PythonScalar minimumScalar = PythonScalar.Create(minimum, "range minimum");
        PythonScalar maximumScalar = PythonScalar.Create(maximum, "range maximum");
        return IsBetween(
            minimumScalar,
            maximumScalar,
            includeMinimum,
            includeMaximum);
    }

    private DaySchedule IsBetween(
        PythonScalar minimum,
        PythonScalar maximum,
        bool includeMinimum,
        bool includeMaximum)
    {
        DaySchedule lower = Compare(
            minimum,
            includeMinimum
                ? PythonComparison.GreaterThanOrEqual
                : PythonComparison.GreaterThan,
            includeMinimum ? "GE" : "GT");
        DaySchedule upper = Compare(
            maximum,
            includeMaximum
                ? PythonComparison.LessThanOrEqual
                : PythonComparison.LessThan,
            includeMaximum ? "LE" : "LT");
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

    public static DaySchedule Where<TFalse>(
        DaySchedule condition,
        DaySchedule whenTrue,
        TFalse whenFalse,
        string? name = null,
        ScheduleType? type = null)
    {
        ScheduleType resultType = ResolveWhereType(condition, type, whenTrue);
        PythonScalar scalar = PythonScalar.Create(whenFalse, "conditional false value");
        return CreateWhere(
            condition,
            index => whenTrue[index],
            _ => scalar.ToScheduleValue(resultType, "conditional false value"),
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

    public static DaySchedule Where<TTrue>(
        DaySchedule condition,
        TTrue whenTrue,
        DaySchedule whenFalse,
        string? name = null,
        ScheduleType? type = null)
    {
        ScheduleType resultType = ResolveWhereType(condition, type, whenFalse);
        PythonScalar scalar = PythonScalar.Create(whenTrue, "conditional true value");
        return CreateWhere(
            condition,
            _ => scalar.ToScheduleValue(resultType, "conditional true value"),
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

    public static DaySchedule Where<TTrue, TFalse>(
        DaySchedule condition,
        TTrue whenTrue,
        TFalse whenFalse,
        string? name = null,
        ScheduleType? type = null)
    {
        ScheduleType resultType = ResolveWhereType(condition, type);
        PythonScalar trueScalar = PythonScalar.Create(whenTrue, "conditional true value");
        PythonScalar falseScalar = PythonScalar.Create(whenFalse, "conditional false value");
        return CreateWhere(
            condition,
            _ => trueScalar.ToScheduleValue(resultType, "conditional true value"),
            _ => falseScalar.ToScheduleValue(resultType, "conditional false value"),
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

    public static DaySchedule operator *(DaySchedule schedule, bool factor)
    {
        return schedule * (factor ? 1d : 0d);
    }

    public static DaySchedule operator *(DaySchedule schedule, char factor)
    {
        throw UnsupportedCharacterScalar("multiplication", factor);
    }

    public static DaySchedule operator *(double factor, DaySchedule schedule)
    {
        return schedule * factor;
    }

    public static DaySchedule operator *(bool factor, DaySchedule schedule)
    {
        return schedule * factor;
    }

    public static DaySchedule operator *(char factor, DaySchedule schedule)
    {
        throw UnsupportedCharacterScalar("reverse multiplication", factor);
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

    public static DaySchedule operator /(DaySchedule schedule, bool divisor)
    {
        return schedule / (divisor ? 1d : 0d);
    }

    public static DaySchedule operator /(DaySchedule schedule, char divisor)
    {
        throw UnsupportedCharacterScalar("division", divisor);
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

    public static DaySchedule operator /(bool numerator, DaySchedule denominator)
    {
        return (numerator ? 1d : 0d) / denominator;
    }

    public static DaySchedule operator /(char numerator, DaySchedule denominator)
    {
        throw UnsupportedCharacterScalar("reverse division", numerator);
    }

    public static DaySchedule operator +(DaySchedule left, DaySchedule right)
    {
        ScheduleType resultType = AdditionType(left.Type, right.Type);
        return left.Zip(right, (a, b) => a + b, resultType, "ADD");
    }

    public static DaySchedule operator +(DaySchedule schedule, double value)
    {
        return AddScalar(schedule, value, FormatPythonFloat(value));
    }

    public static DaySchedule operator +(DaySchedule schedule, int value)
    {
        return AddScalar(schedule, value, FormatPythonInteger(value));
    }

    public static DaySchedule operator +(DaySchedule schedule, uint value)
    {
        return AddScalar(schedule, value, value.ToString(CultureInfo.InvariantCulture));
    }

    public static DaySchedule operator +(DaySchedule schedule, long value)
    {
        return AddScalar(schedule, value, value.ToString(CultureInfo.InvariantCulture));
    }

    public static DaySchedule operator +(DaySchedule schedule, ulong value)
    {
        return AddScalar(schedule, value, value.ToString(CultureInfo.InvariantCulture));
    }

    public static DaySchedule operator +(DaySchedule schedule, bool value)
    {
        return AddScalar(schedule, value ? 1 : 0, FormatPythonBoolean(value));
    }

    public static DaySchedule operator +(DaySchedule schedule, char value)
    {
        throw UnsupportedCharacterScalar("addition", value);
    }

    public static DaySchedule operator +(double value, DaySchedule schedule)
    {
        return schedule + value;
    }

    public static DaySchedule operator +(int value, DaySchedule schedule)
    {
        return schedule + value;
    }

    public static DaySchedule operator +(uint value, DaySchedule schedule)
    {
        return schedule + value;
    }

    public static DaySchedule operator +(long value, DaySchedule schedule)
    {
        return schedule + value;
    }

    public static DaySchedule operator +(ulong value, DaySchedule schedule)
    {
        return schedule + value;
    }

    public static DaySchedule operator +(bool value, DaySchedule schedule)
    {
        return schedule + value;
    }

    public static DaySchedule operator +(char value, DaySchedule schedule)
    {
        throw UnsupportedCharacterScalar("reverse addition", value);
    }

    public static DaySchedule operator -(DaySchedule left, DaySchedule right)
    {
        ScheduleType resultType = AdditionType(left.Type, right.Type);
        return left.Zip(right, (a, b) => a - b, resultType, "SUB");
    }

    public static DaySchedule operator -(DaySchedule schedule, double value)
    {
        return SubtractScalar(schedule, value, FormatPythonFloat(value));
    }

    public static DaySchedule operator -(DaySchedule schedule, int value)
    {
        return SubtractScalar(schedule, value, FormatPythonInteger(value));
    }

    public static DaySchedule operator -(DaySchedule schedule, uint value)
    {
        return SubtractScalar(schedule, value, value.ToString(CultureInfo.InvariantCulture));
    }

    public static DaySchedule operator -(DaySchedule schedule, long value)
    {
        return SubtractScalar(schedule, value, value.ToString(CultureInfo.InvariantCulture));
    }

    public static DaySchedule operator -(DaySchedule schedule, ulong value)
    {
        return SubtractScalar(schedule, value, value.ToString(CultureInfo.InvariantCulture));
    }

    public static DaySchedule operator -(DaySchedule schedule, bool value)
    {
        return SubtractScalar(schedule, value ? 1 : 0, FormatPythonBoolean(value));
    }

    public static DaySchedule operator -(DaySchedule schedule, char value)
    {
        throw UnsupportedCharacterScalar("subtraction", value);
    }

    public static DaySchedule operator -(double value, DaySchedule schedule)
    {
        return ReverseSubtractScalar(value, FormatPythonFloat(value), schedule);
    }

    public static DaySchedule operator -(int value, DaySchedule schedule)
    {
        return ReverseSubtractScalar(value, FormatPythonInteger(value), schedule);
    }

    public static DaySchedule operator -(uint value, DaySchedule schedule)
    {
        return ReverseSubtractScalar(
            value,
            value.ToString(CultureInfo.InvariantCulture),
            schedule);
    }

    public static DaySchedule operator -(long value, DaySchedule schedule)
    {
        return ReverseSubtractScalar(
            value,
            value.ToString(CultureInfo.InvariantCulture),
            schedule);
    }

    public static DaySchedule operator -(ulong value, DaySchedule schedule)
    {
        return ReverseSubtractScalar(
            value,
            value.ToString(CultureInfo.InvariantCulture),
            schedule);
    }

    public static DaySchedule operator -(bool value, DaySchedule schedule)
    {
        return ReverseSubtractScalar(value ? 1 : 0, FormatPythonBoolean(value), schedule);
    }

    public static DaySchedule operator -(char value, DaySchedule schedule)
    {
        throw UnsupportedCharacterScalar("reverse subtraction", value);
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

    private DaySchedule Compare(
        PythonScalar scalar,
        PythonComparison comparison,
        string operation)
    {
        return new DaySchedule(
            $"{Name}:{operation}:{scalar.Text}",
            Values.Select(item => ComparePythonScalar(item, scalar, comparison) ? 1d : 0d),
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
        return new DaySchedule(resultName, Values.Select(operation), resultType);
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
            resultType);
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

    private static DaySchedule AddScalar(DaySchedule schedule, double value, string valueText)
    {
        DomainGuard.NotNull(schedule, nameof(schedule));
        RequireScalarArithmetic(schedule.Type, "addition");
        return schedule.Map(
            item => item + value,
            schedule.Type,
            $"{schedule.Name}:ADD:{valueText}");
    }

    private static DaySchedule SubtractScalar(DaySchedule schedule, double value, string valueText)
    {
        DomainGuard.NotNull(schedule, nameof(schedule));
        RequireScalarArithmetic(schedule.Type, "subtraction");
        return schedule.Map(
            item => item - value,
            schedule.Type,
            $"{schedule.Name}:SUB:{valueText}");
    }

    private static DaySchedule ReverseSubtractScalar(
        double value,
        string valueText,
        DaySchedule schedule)
    {
        DomainGuard.NotNull(schedule, nameof(schedule));
        RequireScalarArithmetic(schedule.Type, "reverse subtraction");
        return new DaySchedule(
            $"{schedule.Name}:SUB:{valueText}",
            schedule.Values.Select(item => value - item),
            schedule.Type);
    }

    private static ScheduleOperationException UnsupportedCharacterScalar(
        string operation,
        char value)
    {
        return new ScheduleOperationException(
            $"Unsupported DaySchedule {operation}: character '{value}' is not a Python numeric scalar.");
    }

    private static double PythonMinimum(double left, double right)
    {
        return right < left ? right : left;
    }

    private static double PythonMinimum(
        double left,
        PythonScalar right,
        ScheduleType resultType)
    {
        return ComparePythonScalar(left, right, PythonComparison.GreaterThan)
            ? right.ToScheduleValue(resultType, "element-wise minimum")
            : left;
    }

    private static double PythonMaximum(double left, double right)
    {
        return right > left ? right : left;
    }

    private static double PythonMaximum(
        double left,
        PythonScalar right,
        ScheduleType resultType)
    {
        return ComparePythonScalar(left, right, PythonComparison.LessThan)
            ? right.ToScheduleValue(resultType, "element-wise maximum")
            : left;
    }

    private static string FormatPythonInteger(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatPythonBoolean(bool value)
    {
        return value ? "True" : "False";
    }

    private static double ConvertPythonIntegerToBinary64(BigInteger integer)
    {
        if (integer.IsZero)
        {
            return 0;
        }

        bool negative = integer.Sign < 0;
        BigInteger magnitude = BigInteger.Abs(integer);
        int bitLength = GetPositiveBitLength(magnitude);
        if (bitLength <= 53)
        {
            double exact = (double)(ulong)magnitude;
            return negative ? -exact : exact;
        }

        int discardedBitCount = bitLength - 53;
        BigInteger retainedInteger = magnitude >> discardedBitCount;
        ulong retained = (ulong)retainedInteger;
        BigInteger remainder = magnitude - (retainedInteger << discardedBitCount);
        BigInteger midpoint = BigInteger.One << (discardedBitCount - 1);
        if (remainder > midpoint || (remainder == midpoint && (retained & 1UL) != 0))
        {
            retained++;
        }

        int exponent = bitLength - 1;
        if (retained == (1UL << 53))
        {
            retained >>= 1;
            exponent++;
        }

        if (exponent > 1023)
        {
            return negative ? double.NegativeInfinity : double.PositiveInfinity;
        }

        const ulong fractionMask = (1UL << 52) - 1;
        ulong bits = ((ulong)(exponent + 1023) << 52) | (retained & fractionMask);
        if (negative)
        {
            bits |= 1UL << 63;
        }

        return BitConverter.Int64BitsToDouble(unchecked((long)bits));
    }

    private static int GetPositiveBitLength(BigInteger value)
    {
        byte[] bytes = value.ToByteArray();
        int mostSignificantIndex = bytes.Length - 1;
        while (mostSignificantIndex > 0 && bytes[mostSignificantIndex] == 0)
        {
            mostSignificantIndex--;
        }

        int highByteBitLength = 0;
        int highByte = bytes[mostSignificantIndex];
        while (highByte != 0)
        {
            highByteBitLength++;
            highByte >>= 1;
        }

        return (mostSignificantIndex * 8) + highByteBitLength;
    }

    private readonly struct PythonScalar
    {
        private PythonScalar(
            double value,
            string text,
            BigInteger? integerValue = null)
        {
            Value = value;
            Text = text;
            IntegerValue = integerValue;
        }

        public double Value { get; }

        public string Text { get; }

        public BigInteger? IntegerValue { get; }

        public double ToPythonFloat(string operation)
        {
            if (IntegerValue.HasValue && double.IsInfinity(Value))
            {
                throw new OverflowException(
                    $"DaySchedule {operation} cannot convert integer {Text} to a finite Python float.");
            }

            return Value;
        }

        public double ToScheduleValue(ScheduleType type, string operation)
        {
            if (IntegerValue.HasValue)
            {
                BigInteger integer = IntegerValue.Value;
                bool outsideDomain = type switch
                {
                    ScheduleType.Temperature => integer < -50 || integer > 200,
                    ScheduleType.OnOff => integer != BigInteger.Zero && integer != BigInteger.One,
                    ScheduleType.Fraction => integer < BigInteger.Zero || integer > BigInteger.One,
                    ScheduleType.Real => false,
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(type),
                        type,
                        "Unknown schedule type."),
                };
                if (outsideDomain)
                {
                    throw new ArgumentOutOfRangeException(
                        operation,
                        Text,
                        $"The integer is outside the {type.CanonicalName()} schedule domain.");
                }
            }

            double scheduleValue = ToPythonFloat(operation);
            return type == ScheduleType.Real
                ? scheduleValue
                : type.ValidateValue(scheduleValue, operation);
        }

        public static PythonScalar Create<T>(T value, string operation)
        {
            object? boxed = value;
            return boxed switch
            {
                bool boolean => new PythonScalar(
                    boolean ? 1 : 0,
                    FormatPythonBoolean(boolean),
                    boolean ? BigInteger.One : BigInteger.Zero),
                sbyte integer => new PythonScalar(
                    integer,
                    integer.ToString(CultureInfo.InvariantCulture),
                    new BigInteger(integer)),
                byte integer => new PythonScalar(
                    integer,
                    integer.ToString(CultureInfo.InvariantCulture),
                    new BigInteger(integer)),
                short integer => new PythonScalar(
                    integer,
                    integer.ToString(CultureInfo.InvariantCulture),
                    new BigInteger(integer)),
                ushort integer => new PythonScalar(
                    integer,
                    integer.ToString(CultureInfo.InvariantCulture),
                    new BigInteger(integer)),
                int integer => new PythonScalar(
                    integer,
                    FormatPythonInteger(integer),
                    new BigInteger(integer)),
                uint integer => new PythonScalar(
                    integer,
                    integer.ToString(CultureInfo.InvariantCulture),
                    new BigInteger(integer)),
                long integer => new PythonScalar(
                    integer,
                    integer.ToString(CultureInfo.InvariantCulture),
                    new BigInteger(integer)),
                ulong integer => new PythonScalar(
                    integer,
                    integer.ToString(CultureInfo.InvariantCulture),
                    new BigInteger(integer)),
                BigInteger integer => new PythonScalar(
                    ConvertPythonIntegerToBinary64(integer),
                    integer.ToString(CultureInfo.InvariantCulture),
                    integer),
                float floating => new PythonScalar(
                    floating,
                    FormatPythonFloat(floating)),
                double floating => new PythonScalar(
                    floating,
                    FormatPythonFloat(floating)),
                _ => throw new ScheduleOperationException(
                    $"Unsupported DaySchedule {operation}: the scalar operand must be a Python-compatible bool, integer, or float value."),
            };
        }
    }

    private enum PythonComparison
    {
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }

    private static bool ComparePythonScalar(
        double left,
        PythonScalar right,
        PythonComparison comparison)
    {
        if (right.IntegerValue.HasValue)
        {
            if (double.IsNaN(left))
            {
                return comparison == PythonComparison.NotEqual;
            }

            int result = ComparePythonFloatToInteger(left, right.IntegerValue.Value);
            return comparison switch
            {
                PythonComparison.Equal => result == 0,
                PythonComparison.NotEqual => result != 0,
                PythonComparison.LessThan => result < 0,
                PythonComparison.LessThanOrEqual => result <= 0,
                PythonComparison.GreaterThan => result > 0,
                PythonComparison.GreaterThanOrEqual => result >= 0,
                _ => throw new InvalidOperationException("Unknown Python comparison."),
            };
        }

        return comparison switch
        {
            PythonComparison.Equal => left == right.Value,
            PythonComparison.NotEqual => left != right.Value,
            PythonComparison.LessThan => left < right.Value,
            PythonComparison.LessThanOrEqual => left <= right.Value,
            PythonComparison.GreaterThan => left > right.Value,
            PythonComparison.GreaterThanOrEqual => left >= right.Value,
            _ => throw new InvalidOperationException("Unknown Python comparison."),
        };
    }

    private static int ComparePythonFloatToInteger(double value, BigInteger integer)
    {
        if (double.IsPositiveInfinity(value))
        {
            return 1;
        }

        if (double.IsNegativeInfinity(value))
        {
            return -1;
        }

        long signedBits = BitConverter.DoubleToInt64Bits(value);
        ulong magnitudeBits = unchecked((ulong)signedBits) & 0x7fff_ffff_ffff_ffffUL;
        if (magnitudeBits == 0)
        {
            return BigInteger.Zero.CompareTo(integer);
        }

        ulong fraction = magnitudeBits & 0x000f_ffff_ffff_ffffUL;
        int exponentBits = (int)((magnitudeBits >> 52) & 0x7ffUL);
        BigInteger significand = exponentBits == 0
            ? new BigInteger(fraction)
            : new BigInteger(fraction | (1UL << 52));
        if (signedBits < 0)
        {
            significand = -significand;
        }

        int binaryExponent = exponentBits == 0
            ? -1074
            : exponentBits - 1023 - 52;
        return binaryExponent >= 0
            ? (significand << binaryExponent).CompareTo(integer)
            : significand.CompareTo(integer << -binaryExponent);
    }

    private static string FormatPythonFloat(double value)
    {
        if (double.IsNaN(value))
        {
            return "nan";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "inf";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-inf";
        }

        if (value == 0)
        {
            return BitConverter.DoubleToInt64Bits(value) < 0 ? "-0.0" : "0.0";
        }

        long signedBits = BitConverter.DoubleToInt64Bits(value);
        bool negative = signedBits < 0;
        ulong magnitudeBits = unchecked((ulong)signedBits) & 0x7fff_ffff_ffff_ffffUL;
        ulong fraction = magnitudeBits & 0x000f_ffff_ffff_ffffUL;
        int exponentBits = (int)((magnitudeBits >> 52) & 0x7ffUL);
        BigInteger significand;
        int binaryExponent;
        if (exponentBits == 0)
        {
            significand = new BigInteger(fraction);
            binaryExponent = -1074;
        }
        else
        {
            significand = new BigInteger(fraction | (1UL << 52));
            binaryExponent = exponentBits - 1023 - 52;
        }

        BigInteger numerator = significand;
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

        bool midpointInclusive = significand.IsEven;
        BigInteger lowerBoundaryCoefficient;
        int lowerBoundaryExponent;
        if (exponentBits > 1 && fraction == 0)
        {
            lowerBoundaryCoefficient = (significand << 2) - BigInteger.One;
            lowerBoundaryExponent = binaryExponent - 2;
        }
        else
        {
            lowerBoundaryCoefficient = (significand << 1) - BigInteger.One;
            lowerBoundaryExponent = binaryExponent - 1;
        }

        BigInteger upperBoundaryCoefficient = (significand << 1) + BigInteger.One;
        int upperBoundaryExponent = binaryExponent - 1;
        for (int precision = 1; precision <= 17; precision++)
        {
            int decimalScale = decimalExponent - precision + 1;
            BigInteger minimumCandidate = MinimumDecimalCandidate(
                lowerBoundaryCoefficient,
                lowerBoundaryExponent,
                decimalScale,
                midpointInclusive);
            BigInteger maximumCandidate = MaximumDecimalCandidate(
                upperBoundaryCoefficient,
                upperBoundaryExponent,
                decimalScale,
                midpointInclusive);
            if (minimumCandidate > maximumCandidate)
            {
                continue;
            }

            BigInteger candidate = RoundRationalAtDecimalScale(
                numerator,
                denominator,
                decimalScale);
            if (candidate < minimumCandidate)
            {
                candidate = minimumCandidate;
            }
            else if (candidate > maximumCandidate)
            {
                candidate = maximumCandidate;
            }

            int normalizedScale = decimalScale;
            while (BigInteger.Remainder(candidate, 10) == BigInteger.Zero)
            {
                candidate /= 10;
                normalizedScale++;
            }

            string digits = candidate.ToString(CultureInfo.InvariantCulture);
            string rendered = RenderPythonFloat(digits, normalizedScale);
            return negative ? "-" + rendered : rendered;
        }

        throw new InvalidOperationException(
            "A Python-compatible floating-point representation could not be produced.");
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

    private static BigInteger RoundRationalAtDecimalScale(
        BigInteger numerator,
        BigInteger denominator,
        int decimalScale)
    {
        BigInteger scaledNumerator = numerator;
        BigInteger scaledDenominator = denominator;
        if (decimalScale >= 0)
        {
            scaledDenominator *= BigInteger.Pow(10, decimalScale);
        }
        else
        {
            scaledNumerator *= BigInteger.Pow(10, -decimalScale);
        }

        BigInteger quotient = BigInteger.DivRem(
            scaledNumerator,
            scaledDenominator,
            out BigInteger remainder);
        int midpointComparison = (remainder << 1).CompareTo(scaledDenominator);
        if (midpointComparison > 0
            || (midpointComparison == 0 && !quotient.IsEven))
        {
            quotient++;
        }

        return quotient;
    }

    private static BigInteger MinimumDecimalCandidate(
        BigInteger boundaryCoefficient,
        int binaryExponent,
        int decimalScale,
        bool inclusive)
    {
        BigInteger floor = DivideDyadicAtDecimalScale(
            boundaryCoefficient,
            binaryExponent,
            decimalScale,
            out BigInteger remainder);
        return remainder != BigInteger.Zero || !inclusive
            ? floor + BigInteger.One
            : floor;
    }

    private static BigInteger MaximumDecimalCandidate(
        BigInteger boundaryCoefficient,
        int binaryExponent,
        int decimalScale,
        bool inclusive)
    {
        BigInteger floor = DivideDyadicAtDecimalScale(
            boundaryCoefficient,
            binaryExponent,
            decimalScale,
            out BigInteger remainder);
        return remainder == BigInteger.Zero && !inclusive
            ? floor - BigInteger.One
            : floor;
    }

    private static BigInteger DivideDyadicAtDecimalScale(
        BigInteger coefficient,
        int binaryExponent,
        int decimalScale,
        out BigInteger remainder)
    {
        BigInteger numerator = coefficient;
        BigInteger denominator = BigInteger.One;
        if (binaryExponent >= 0)
        {
            numerator <<= binaryExponent;
        }
        else
        {
            denominator <<= -binaryExponent;
        }

        if (decimalScale >= 0)
        {
            denominator *= BigInteger.Pow(10, decimalScale);
        }
        else
        {
            numerator *= BigInteger.Pow(10, -decimalScale);
        }

        return BigInteger.DivRem(numerator, denominator, out remainder);
    }

    private static string RenderPythonFloat(string digits, int decimalScale)
    {
        int exponent = decimalScale + digits.Length - 1;
        if (exponent < -4 || exponent >= 16)
        {
            string mantissa = digits.Length == 1
                ? digits
                : InsertDecimalPoint(digits, 1);
            string sign = exponent >= 0 ? "+" : "-";
            string exponentDigits = Math.Abs(exponent).ToString("D2", CultureInfo.InvariantCulture);
            return mantissa + "e" + sign + exponentDigits;
        }

        int decimalPosition = exponent + 1;
        if (decimalPosition <= 0)
        {
            return "0." + new string('0', -decimalPosition) + digits;
        }

        if (decimalPosition >= digits.Length)
        {
            return digits + new string('0', decimalPosition - digits.Length) + ".0";
        }

        return InsertDecimalPoint(digits, decimalPosition);
    }

    private static string InsertDecimalPoint(string digits, int position)
    {
        char[] result = new char[digits.Length + 1];
        digits.CopyTo(0, result, 0, position);
        result[position] = '.';
        digits.CopyTo(position, result, position + 1, digits.Length - position);
        return new string(result);
    }
}
