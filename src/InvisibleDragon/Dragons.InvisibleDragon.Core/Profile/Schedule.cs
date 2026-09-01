using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Profile;

public sealed record SchedulePeriod
{
    public SchedulePeriod(DateTime start, DateTime end, RuleSet ruleSet)
    {
        Start = Schedule.NormalizeDate(start);
        End = Schedule.NormalizeDate(end);
        if (End < Start)
        {
            throw new ArgumentException("A schedule period cannot end before it starts.");
        }

        RuleSet = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));
    }

    public SchedulePeriod(string start, string end, RuleSet ruleSet)
        : this(Schedule.ParseDate(start, nameof(start)), Schedule.ParseDate(end, nameof(end)), ruleSet)
    {
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public RuleSet RuleSet { get; }
}

public sealed record ScheduleWindow
{
    public ScheduleWindow(DateTime start, DateTime end, RuleSet ruleSet)
    {
        Start = Schedule.NormalizeDate(start);
        End = Schedule.NormalizeDate(end);
        if (End < Start)
        {
            throw new ArgumentException("A schedule window cannot end before it starts.");
        }

        RuleSet = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));
    }

    public ScheduleWindow(string start, string end, RuleSet ruleSet)
        : this(Schedule.ParseDate(start, nameof(start)), Schedule.ParseDate(end, nameof(end)), ruleSet)
    {
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public RuleSet RuleSet { get; }
}

/// <summary>
/// One date window whose value can be a Python-compatible scalar,
/// <see cref="DaySchedule"/>, or <see cref="RuleSet"/>.
/// </summary>
public sealed record ScheduleValueWindow
{
    public ScheduleValueWindow(DateTime start, DateTime end, object value)
    {
        Start = Schedule.NormalizeDate(start);
        End = Schedule.NormalizeDate(end);
        if (End < Start)
        {
            throw new ArgumentException("A schedule window cannot end before it starts.");
        }

        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public ScheduleValueWindow(string start, string end, object value)
        : this(Schedule.ParseDate(start, nameof(start)), Schedule.ParseDate(end, nameof(end)), value)
    {
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public object Value { get; }
}

/// <summary>
/// An immutable non-leap-year schedule with one rule set for every day of 2026.
/// </summary>
public sealed class Schedule : IReadOnlyList<RuleSet>, IEquatable<Schedule>
{
    public const int DefaultYear = 2026;

    public const int FixedLength = 365;

    private static readonly DateTime FirstDay = new(DefaultYear, 1, 1);

    private static readonly IReadOnlyList<DateTime> AnnualTimeTuple =
        new ReadOnlyCollection<DateTime>(
            Enumerable.Range(0, FixedLength)
                .Select(index => FirstDay.AddDays(index))
                .ToArray());

    public Schedule(
        string? name,
        IEnumerable<RuleSet>? ruleSets = null,
        ScheduleType? type = null)
    {
        Name = NormalizeScheduleName(name);

        if (ruleSets is null)
        {
            ScheduleType defaultType = type ?? ScheduleType.Real;
            RuleSets = new ReadOnlyCollection<RuleSet>(
                Enumerable.Range(0, FixedLength)
                    .Select(index => CreateZeroRuleSet(Name, index, defaultType))
                    .ToArray());
            Type = defaultType;
            return;
        }

        RuleSet[] copy = ruleSets.ToArray();
        if (copy.Length != FixedLength)
        {
            throw new ArgumentException($"An annual schedule requires exactly {FixedLength} rule sets.", nameof(ruleSets));
        }

        if (copy.Any(ruleSet => ruleSet is null))
        {
            throw new ArgumentException("An annual schedule cannot contain null rule sets.", nameof(ruleSets));
        }

        Type = type ?? copy[0].Type;
        if (copy.Any(ruleSet => ruleSet.Type != Type))
        {
            throw new ArgumentException("An annual schedule cannot mix rule-set types.", nameof(ruleSets));
        }

        RuleSets = new ReadOnlyCollection<RuleSet>(copy);
    }

    public string Name { get; }

    public ScheduleType Type { get; }

    public IReadOnlyList<RuleSet> RuleSets { get; }

    /// <summary>
    /// Gets the pinned non-leap-year dates corresponding to the 365 schedule slots.
    /// </summary>
    public static IReadOnlyList<DateTime> TimeTuple => AnnualTimeTuple;

    public int Count => FixedLength;

    public RuleSet this[int index] => RuleSets[index];

    public RuleSet this[DateTime date] => RuleSets[(NormalizeDate(date) - FirstDay).Days];

    public double Minimum => RuleSets.Min(ruleSet => ruleSet.Minimum);

    public double Maximum => RuleSets.Max(ruleSet => ruleSet.Maximum);

    public IReadOnlyList<DaySchedule> DaySchedules =>
        new ReadOnlyCollection<DaySchedule>(
            Enumerable.Range(0, FixedLength)
                .Select(index => RuleSets[index].GetDaySchedule(AnnualTimeTuple[index].DayOfWeek))
                .ToArray());

    public double IntegralHours => Python312FloatSum(
        Enumerable.Range(0, FixedLength)
            .Select(index => RuleSets[index]
                .GetDaySchedule(AnnualTimeTuple[index].DayOfWeek)
                .IntegralHours));

    public double Integral => IntegralHours;

    // Preserve the pinned Python 0.7.0 contract: this is the mean daily
    // integral, despite the historical property name suggesting a value mean.
    public double Average => IntegralHours / FixedLength;

    public double PositiveAverage
    {
        get
        {
            double[] positiveValues = DaySchedules
                .SelectMany(daySchedule => daySchedule)
                .Where(value => value > 0)
                .ToArray();
            return positiveValues.Length == 0
                ? 0
                : Python312FloatSum(positiveValues) / positiveValues.Length;
        }
    }

    public static Schedule Constant(string? name, double value, ScheduleType type = ScheduleType.Real)
    {
        return FromConstant(name, value, type);
    }

    public static Schedule Constant<T>(string? name, T value, ScheduleType type = ScheduleType.Real)
    {
        return FromConstant(name, value, type);
    }

    public static Schedule Constant(
        string? name,
        DaySchedule value,
        ScheduleType type = ScheduleType.Real)
    {
        return FromConstant(name, value, type);
    }

    public static Schedule Constant(
        string? name,
        RuleSet value,
        ScheduleType type = ScheduleType.Real)
    {
        return FromConstant(name, value, type);
    }

    public static Schedule FromConstant<T>(
        string? name,
        T value,
        ScheduleType type = ScheduleType.Real)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return FromConstantObject(name, value, type, nameof(value));
    }

    public static Schedule FromConstant(
        string? name,
        DaySchedule value,
        ScheduleType type = ScheduleType.Real)
    {
        DomainGuard.NotNull(value, nameof(value));
        string factoryName = NormalizeScheduleName(name);
        RuleSet ruleSet = RuleSet.FromDaySchedule($"{factoryName}:ruleset", value);
        return new Schedule(factoryName, Enumerable.Repeat(ruleSet, FixedLength), value.Type);
    }

    public static Schedule FromConstant(
        string? name,
        RuleSet value,
        ScheduleType type = ScheduleType.Real)
    {
        DomainGuard.NotNull(value, nameof(value));
        return new Schedule(
            NormalizeScheduleName(name),
            Enumerable.Repeat(value, FixedLength),
            value.Type);
    }

    public static Schedule FromCompact(string? name, IEnumerable<SchedulePeriod> periods)
    {
        DomainGuard.NotNull(periods, nameof(periods));
        string scheduleName = NormalizeScheduleName(name);
        SchedulePeriod[] copy = periods.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("At least one annual schedule period is required.", nameof(periods));
        }

        if (copy.Any(period => period is null))
        {
            throw new ArgumentException("Annual schedule periods cannot contain null.", nameof(periods));
        }

        ScheduleType type = copy[0].RuleSet.Type;
        if (copy.Any(period => period.RuleSet.Type != type))
        {
            throw new ArgumentException("Annual schedule periods cannot mix schedule types.", nameof(periods));
        }

        RuleSet?[] values = new RuleSet?[FixedLength];
        foreach (SchedulePeriod period in copy)
        {
            int start = (period.Start - FirstDay).Days;
            int end = (period.End - FirstDay).Days;
            for (int index = start; index <= end; index++)
            {
                values[index] = period.RuleSet;
            }
        }

        for (int index = 0; index < values.Length; index++)
        {
            values[index] ??= CreateZeroRuleSet(scheduleName, index, type);
        }

        return new Schedule(scheduleName, values.Select(value => value!).ToArray(), type);
    }

    public static Schedule FromWindows(
        string? name,
        RuleSet defaultRuleSet,
        IEnumerable<ScheduleWindow> windows)
    {
        DomainGuard.NotNull(defaultRuleSet, nameof(defaultRuleSet));
        DomainGuard.NotNull(windows, nameof(windows));

        ScheduleWindow[] copy = windows.ToArray();
        if (copy.Any(window => window is null || window.RuleSet.Type != defaultRuleSet.Type))
        {
            throw new ArgumentException("Schedule windows cannot be null or mix schedule types.", nameof(windows));
        }

        RuleSet[] values = new RuleSet[FixedLength];
        for (int index = 0; index < FixedLength; index++)
        {
            DateTime date = FirstDay.AddDays(index);
            ScheduleWindow? match = copy.LastOrDefault(window => window.Start <= date && date <= window.End);
            values[index] = match?.RuleSet ?? defaultRuleSet;
        }

        return new Schedule(NormalizeScheduleName(name), values, defaultRuleSet.Type);
    }

    public static Schedule FromWindows(
        string? name,
        object defaultValue,
        IEnumerable<ScheduleValueWindow> windows,
        ScheduleType? type = null)
    {
        DomainGuard.NotNull(defaultValue, nameof(defaultValue));
        DomainGuard.NotNull(windows, nameof(windows));
        ScheduleValueWindow[] copy = windows.ToArray();
        if (copy.Any(window => window is null))
        {
            throw new ArgumentException("Schedule windows cannot contain null.", nameof(windows));
        }

        Schedule schedule = FromConstantObject(name, defaultValue, type, "defaultValue");
        for (int index = 0; index < copy.Length; index++)
        {
            ScheduleValueWindow window = copy[index];
            RuleSet ruleSet = CoerceRuleSet(
                window.Value,
                schedule.Type,
                $"{schedule.Name}:window:{index + 1:D3}",
                nameof(windows));
            schedule = schedule.Apply(ruleSet, window.Start, window.End);
        }

        return schedule;
    }

    public Schedule Apply(RuleSet ruleSet, DateTime start, DateTime end, string? name = null)
    {
        DomainGuard.NotNull(ruleSet, nameof(ruleSet));
        if (ruleSet.Type != Type)
        {
            throw new ArgumentException("The applied rule set must match the schedule type.", nameof(ruleSet));
        }

        DateTime normalizedStart = NormalizeDate(start);
        DateTime normalizedEnd = NormalizeDate(end);
        if (normalizedEnd < normalizedStart)
        {
            throw new ArgumentException("The applied range cannot end before it starts.");
        }

        RuleSet[] copy = RuleSets.ToArray();
        for (int index = (normalizedStart - FirstDay).Days; index <= (normalizedEnd - FirstDay).Days; index++)
        {
            copy[index] = ruleSet;
        }

        return new Schedule(name ?? Name, copy, Type);
    }

    public Schedule Apply(RuleSet ruleSet, string start, string end, string? name = null)
    {
        return Apply(
            ruleSet,
            ParseDate(start, nameof(start)),
            ParseDate(end, nameof(end)),
            name);
    }

    public Schedule AsType(ScheduleType type)
    {
        return MapCompact(ruleSet => DeepCopyRuleSet(ruleSet).AsType(type), Name);
    }

    public Schedule DeepCopy()
    {
        return FromCompact(
            $"{Name}:COPY",
            Compactize().Select(period => new SchedulePeriod(
                period.Start,
                period.End,
                DeepCopyRuleSet(period.RuleSet))));
    }

    public Schedule Add(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.Add(right), "ADD", name);
    }

    public Schedule Add(double value, string? name = null)
    {
        return AddPythonScalar(value, name);
    }

    public Schedule Add<T>(T value, string? name = null)
    {
        return AddPythonScalar(value, name);
    }

    public Schedule ReverseAdd(double value, string? name = null)
    {
        return ReverseAddPythonScalar(value, name);
    }

    public Schedule ReverseAdd<T>(T value, string? name = null)
    {
        return ReverseAddPythonScalar(value, name);
    }

    public Schedule Subtract(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.Subtract(right), "SUB", name);
    }

    public Schedule Subtract(double value, string? name = null)
    {
        return SubtractPythonScalar(value, name);
    }

    public Schedule Subtract<T>(T value, string? name = null)
    {
        return SubtractPythonScalar(value, name);
    }

    public Schedule ReverseSubtract(double value, string? name = null)
    {
        return ReverseSubtractPythonScalar(value, name);
    }

    public Schedule ReverseSubtract<T>(T value, string? name = null)
    {
        return ReverseSubtractPythonScalar(value, name);
    }

    public Schedule Divide(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.Divide(right), "DIV", name);
    }

    public Schedule Divide(double value, string? name = null)
    {
        return DividePythonScalar(value, name);
    }

    public Schedule Divide<T>(T value, string? name = null)
    {
        return DividePythonScalar(value, name);
    }

    public Schedule ReverseDivide(double value, string? name = null)
    {
        return ReverseDividePythonScalar(value, name);
    }

    public Schedule ReverseDivide<T>(T value, string? name = null)
    {
        return ReverseDividePythonScalar(value, name);
    }

    public Schedule Multiply(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.Multiply(right), "MUL", name);
    }

    public Schedule Multiply(double value, string? name = null)
    {
        return MultiplyPythonScalar(value, name);
    }

    public Schedule Multiply<T>(T value, string? name = null)
    {
        return MultiplyPythonScalar(value, name);
    }

    public Schedule LogicalAnd(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.LogicalAnd(right), "AND", name);
    }

    public Schedule LogicalOr(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.LogicalOr(right), "OR", name);
    }

    public Schedule Invert(string? name = null)
    {
        return MapCompact(ruleSet => ruleSet.Invert(), name ?? $"{Name}:INVERTED");
    }

    public Schedule ElementEqual(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.ElementEqual(right), "EQ", name);
    }

    public Schedule ElementEqual(double value, string? name = null)
    {
        return ElementEqualPythonScalar(value, name);
    }

    public Schedule ElementEqual<T>(T value, string? name = null)
    {
        return ElementEqualPythonScalar(value, name);
    }

    public Schedule ElementNotEqual(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.ElementNotEqual(right), "NE", name);
    }

    public Schedule ElementNotEqual(double value, string? name = null)
    {
        return ElementNotEqualPythonScalar(value, name);
    }

    public Schedule ElementNotEqual<T>(T value, string? name = null)
    {
        return ElementNotEqualPythonScalar(value, name);
    }

    public Schedule LessThan(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.LessThan(right), "LT", name);
    }

    public Schedule LessThan(double value, string? name = null)
    {
        return LessThanPythonScalar(value, name);
    }

    public Schedule LessThan<T>(T value, string? name = null)
    {
        return LessThanPythonScalar(value, name);
    }

    public Schedule LessThanOrEqual(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.LessThanOrEqual(right), "LE", name);
    }

    public Schedule LessThanOrEqual(double value, string? name = null)
    {
        return LessThanOrEqualPythonScalar(value, name);
    }

    public Schedule LessThanOrEqual<T>(T value, string? name = null)
    {
        return LessThanOrEqualPythonScalar(value, name);
    }

    public Schedule GreaterThan(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.GreaterThan(right), "GT", name);
    }

    public Schedule GreaterThan(double value, string? name = null)
    {
        return GreaterThanPythonScalar(value, name);
    }

    public Schedule GreaterThan<T>(T value, string? name = null)
    {
        return GreaterThanPythonScalar(value, name);
    }

    public Schedule GreaterThanOrEqual(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.GreaterThanOrEqual(right), "GE", name);
    }

    public Schedule GreaterThanOrEqual(double value, string? name = null)
    {
        return GreaterThanOrEqualPythonScalar(value, name);
    }

    public Schedule GreaterThanOrEqual<T>(T value, string? name = null)
    {
        return GreaterThanOrEqualPythonScalar(value, name);
    }

    public Schedule ElementMinimum(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.ElementMinimum(right), "MIN", name);
    }

    public Schedule ElementMaximum(Schedule other, string? name = null)
    {
        return Combine(other, (left, right) => left.ElementMaximum(right), "MAX", name);
    }

    public Schedule IsOn(string? name = null)
    {
        return ElementEqual(1, name);
    }

    public Schedule IsOff(string? name = null)
    {
        return ElementEqual(0, name);
    }

    public Schedule IsPositive(string? name = null)
    {
        return GreaterThan(0, name);
    }

    public Schedule IsNegative(string? name = null)
    {
        return LessThan(0, name);
    }

    public Schedule IsZero(string? name = null)
    {
        return ElementEqual(0, name);
    }

    public Schedule IsNonzero(string? name = null)
    {
        return ElementNotEqual(0, name);
    }

    public Schedule IsBetween(
        double minimum,
        double maximum,
        bool includeMinimum = true,
        bool includeMaximum = true,
        string? name = null)
    {
        Schedule lower = includeMinimum ? GreaterThanOrEqual(minimum) : GreaterThan(minimum);
        Schedule upper = includeMaximum ? LessThanOrEqual(maximum) : LessThan(maximum);
        return lower.LogicalAnd(upper, name);
    }

    public Schedule IsBetween<TMinimum, TMaximum>(
        TMinimum minimum,
        TMaximum maximum,
        bool includeMinimum = true,
        bool includeMaximum = true,
        string? name = null)
    {
        Schedule lower = includeMinimum
            ? GreaterThanOrEqualPythonScalar(minimum, null)
            : GreaterThanPythonScalar(minimum, null);
        Schedule upper = includeMaximum
            ? LessThanOrEqualPythonScalar(maximum, null)
            : LessThanPythonScalar(maximum, null);
        return lower.LogicalAnd(upper, name);
    }

    public static Schedule Where(
        Schedule condition,
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
            throw new ScheduleOperationException("Schedule.Where requires an OnOff condition schedule.");
        }

        var compactizedSchedules = new List<IReadOnlyList<SchedulePeriod>>
        {
            condition.Compactize(),
        };
        int? trueScheduleIndex = null;
        int? falseScheduleIndex = null;
        if (whenTrue is Schedule trueSchedule)
        {
            trueScheduleIndex = compactizedSchedules.Count;
            compactizedSchedules.Add(trueSchedule.Compactize());
        }

        if (whenFalse is Schedule falseSchedule)
        {
            falseScheduleIndex = compactizedSchedules.Count;
            compactizedSchedules.Add(falseSchedule.Compactize());
        }

        IReadOnlyList<IReadOnlyList<SchedulePeriod>> unified =
            UnifyCompactizedSchedulesMany(compactizedSchedules.ToArray());
        string resultName = name is null || name.Length == 0
            ? "WHERE"
            : DomainGuard.RequiredText(name, nameof(name));

        return FromCompact(
            resultName,
            Enumerable.Range(0, unified[0].Count).Select(index => new SchedulePeriod(
                unified[0][index].Start,
                unified[0][index].End,
                RuleSet.Where(
                    unified[0][index].RuleSet,
                    trueScheduleIndex.HasValue
                        ? unified[trueScheduleIndex.Value][index].RuleSet
                        : whenTrue,
                    falseScheduleIndex.HasValue
                        ? unified[falseScheduleIndex.Value][index].RuleSet
                        : whenFalse,
                    type: type))));
    }

    public Schedule Scale(double factor, string? name = null)
    {
        return Multiply(factor, name);
    }

    public Schedule NormalizeByMaximum(string? name = null)
    {
        return MapCompact(
            ruleSet => ruleSet.NormalizeByMaximum(),
            name ?? $"{Name}_normalized");
    }

    public Schedule Clip(double? minimum = null, double? maximum = null, string? name = null)
    {
        return MapCompact(
            ruleSet => ruleSet.Clip(minimum, maximum),
            string.IsNullOrEmpty(name) ? $"{Name}:CLIP" : name!);
    }

    public IReadOnlyList<SchedulePeriod> Compactize()
    {
        List<SchedulePeriod> periods = new();
        for (int index = 0; index < FixedLength; index++)
        {
            DateTime date = FirstDay.AddDays(index);
            // Python RuleSet does not define value equality, so compactization
            // joins only adjacent dates backed by the same RuleSet instance.
            if (index == 0 || !ReferenceEquals(RuleSets[index], RuleSets[index - 1]))
            {
                periods.Add(new SchedulePeriod(date, date, RuleSets[index]));
            }
            else
            {
                SchedulePeriod previous = periods[periods.Count - 1];
                periods[periods.Count - 1] = new SchedulePeriod(previous.Start, date, previous.RuleSet);
            }
        }

        return new ReadOnlyCollection<SchedulePeriod>(periods);
    }

    public static (
        IReadOnlyList<SchedulePeriod> Left,
        IReadOnlyList<SchedulePeriod> Right) UnifyCompactizedSchedules(
            IReadOnlyList<SchedulePeriod> left,
            IReadOnlyList<SchedulePeriod> right)
    {
        DomainGuard.NotNull(left, nameof(left));
        DomainGuard.NotNull(right, nameof(right));
        IReadOnlyList<IReadOnlyList<SchedulePeriod>> unified =
            UnifyCompactizedSchedulesMany(left, right);
        return (unified[0], unified[1]);
    }

    public static IReadOnlyList<IReadOnlyList<SchedulePeriod>> UnifyCompactizedSchedulesMany(
        params IReadOnlyList<SchedulePeriod>[] compactizedSchedules)
    {
        DomainGuard.NotNull(compactizedSchedules, nameof(compactizedSchedules));
        if (compactizedSchedules.Length == 0)
        {
            return Array.Empty<IReadOnlyList<SchedulePeriod>>();
        }

        if (compactizedSchedules.Any(schedule => schedule is null))
        {
            throw new ArgumentException("Compactized schedules cannot contain null collections.", nameof(compactizedSchedules));
        }

        var boundaries = new SortedSet<int>();
        foreach (IReadOnlyList<SchedulePeriod> schedule in compactizedSchedules)
        {
            foreach (SchedulePeriod period in schedule)
            {
                if (period is null)
                {
                    throw new ArgumentException("Compactized schedules cannot contain null periods.", nameof(compactizedSchedules));
                }

                int startIndex = (period.Start - FirstDay).Days;
                int endExclusiveIndex = (period.End - FirstDay).Days + 1;
                boundaries.Add(startIndex);
                boundaries.Add(endExclusiveIndex);
            }
        }

        int[] orderedBoundaries = boundaries.ToArray();
        var results = compactizedSchedules
            .Select(_ => new List<SchedulePeriod>())
            .ToArray();
        for (int boundaryIndex = 0; boundaryIndex < orderedBoundaries.Length - 1; boundaryIndex++)
        {
            int startIndex = orderedBoundaries[boundaryIndex];
            int endIndex = orderedBoundaries[boundaryIndex + 1] - 1;
            DateTime start = FirstDay.AddDays(startIndex);
            DateTime end = FirstDay.AddDays(endIndex);

            for (int scheduleIndex = 0; scheduleIndex < compactizedSchedules.Length; scheduleIndex++)
            {
                RuleSet ruleSet = FindRuleSet(compactizedSchedules[scheduleIndex], start);
                results[scheduleIndex].Add(new SchedulePeriod(start, end, ruleSet));
            }
        }

        return new ReadOnlyCollection<IReadOnlyList<SchedulePeriod>>(
            results
                .Select(result => (IReadOnlyList<SchedulePeriod>)new ReadOnlyCollection<SchedulePeriod>(result))
                .ToArray());
    }

    public IdfObject ToIdfObject()
    {
        var fields = new List<string?>
        {
            Name,
            Type.IdfObjectName(),
        };

        foreach (SchedulePeriod period in Compactize())
        {
            fields.Add($"Through: {period.End.Month}/{period.End.Day}");
            AddRuleSetIdfFields(fields, period.RuleSet);
        }

        return new IdfObject("Schedule:Compact", fields);
    }

    public string Summary(int maxPeriods = 8)
    {
        IReadOnlyList<SchedulePeriod> compact = Compactize();
        int uniqueRuleSets = RuleSets
            .Select(ruleSet => ruleSet.Name)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var lines = new List<string>
        {
            $"Schedule {PythonRepr(Name)} [type={Type.CanonicalName()}, days={FixedLength}]",
            $"  range: min={FormatPythonGeneral(Minimum)}, max={FormatPythonGeneral(Maximum)}, "
                + $"periods={compact.Count}, unique_rulesets={uniqueRuleSets}",
        };

        int previewCount = maxPeriods >= 0
            ? Math.Min(maxPeriods, compact.Count)
            : Math.Max(compact.Count + maxPeriods, 0);
        for (int index = 0; index < previewCount; index++)
        {
            SchedulePeriod period = compact[index];
            lines.Add(
                $"  {period.Start.Month:00}/{period.Start.Day:00} ~ "
                + $"{period.End.Month:00}/{period.End.Day:00}: "
                + $"{PythonRepr(period.RuleSet.Name)} "
                + $"(min={FormatPythonGeneral(period.RuleSet.Minimum)}, "
                + $"max={FormatPythonGeneral(period.RuleSet.Maximum)})");
        }

        if (compact.Count > maxPeriods)
        {
            long hiddenCount = (long)compact.Count - maxPeriods;
            lines.Add($"  ... ({hiddenCount} more periods)");
        }

        return string.Join("\n", lines);
    }

    public override string ToString()
    {
        return Summary();
    }

    public static Schedule operator *(Schedule left, Schedule right)
    {
        return left.Multiply(right);
    }

    public static Schedule operator *(Schedule schedule, double value)
    {
        return schedule.Multiply(value);
    }

    public static Schedule operator *(Schedule schedule, int value) => schedule.Multiply(value);

    public static Schedule operator *(Schedule schedule, uint value) => schedule.Multiply(value);

    public static Schedule operator *(Schedule schedule, long value) => schedule.Multiply(value);

    public static Schedule operator *(Schedule schedule, ulong value) => schedule.Multiply(value);

    public static Schedule operator *(Schedule schedule, BigInteger value) => schedule.Multiply(value);

    public static Schedule operator *(Schedule schedule, bool value) => schedule.Multiply(value);

    public static Schedule operator *(Schedule schedule, char value) => schedule.Multiply(value);

    public static Schedule operator *(double value, Schedule schedule)
    {
        return schedule.Multiply(value);
    }

    public static Schedule operator *(int value, Schedule schedule) => schedule.Multiply(value);

    public static Schedule operator *(uint value, Schedule schedule) => schedule.Multiply(value);

    public static Schedule operator *(long value, Schedule schedule) => schedule.Multiply(value);

    public static Schedule operator *(ulong value, Schedule schedule) => schedule.Multiply(value);

    public static Schedule operator *(BigInteger value, Schedule schedule) => schedule.Multiply(value);

    public static Schedule operator *(bool value, Schedule schedule) => schedule.Multiply(value);

    public static Schedule operator *(char value, Schedule schedule) => schedule.Multiply(value);

    public static Schedule operator /(Schedule left, Schedule right)
    {
        return left.Divide(right);
    }

    public static Schedule operator /(Schedule schedule, double value)
    {
        return schedule.Divide(value);
    }

    public static Schedule operator /(Schedule schedule, int value) => schedule.Divide(value);

    public static Schedule operator /(Schedule schedule, uint value) => schedule.Divide(value);

    public static Schedule operator /(Schedule schedule, long value) => schedule.Divide(value);

    public static Schedule operator /(Schedule schedule, ulong value) => schedule.Divide(value);

    public static Schedule operator /(Schedule schedule, BigInteger value) => schedule.Divide(value);

    public static Schedule operator /(Schedule schedule, bool value) => schedule.Divide(value);

    public static Schedule operator /(Schedule schedule, char value) => schedule.Divide(value);

    public static Schedule operator /(double value, Schedule schedule)
    {
        return schedule.ReverseDivide(value);
    }

    public static Schedule operator /(int value, Schedule schedule) => schedule.ReverseDivide(value);

    public static Schedule operator /(uint value, Schedule schedule) => schedule.ReverseDivide(value);

    public static Schedule operator /(long value, Schedule schedule) => schedule.ReverseDivide(value);

    public static Schedule operator /(ulong value, Schedule schedule) => schedule.ReverseDivide(value);

    public static Schedule operator /(BigInteger value, Schedule schedule) => schedule.ReverseDivide(value);

    public static Schedule operator /(bool value, Schedule schedule) => schedule.ReverseDivide(value);

    public static Schedule operator /(char value, Schedule schedule) => schedule.ReverseDivide(value);

    public static Schedule operator +(Schedule left, Schedule right)
    {
        return left.Add(right);
    }

    public static Schedule operator +(Schedule schedule, double value)
    {
        return schedule.Add(value);
    }

    public static Schedule operator +(Schedule schedule, int value) => schedule.Add(value);

    public static Schedule operator +(Schedule schedule, uint value) => schedule.Add(value);

    public static Schedule operator +(Schedule schedule, long value) => schedule.Add(value);

    public static Schedule operator +(Schedule schedule, ulong value) => schedule.Add(value);

    public static Schedule operator +(Schedule schedule, BigInteger value) => schedule.Add(value);

    public static Schedule operator +(Schedule schedule, bool value) => schedule.Add(value);

    public static Schedule operator +(Schedule schedule, char value) => schedule.Add(value);

    public static Schedule operator +(double value, Schedule schedule)
    {
        return schedule.ReverseAdd(value);
    }

    public static Schedule operator +(int value, Schedule schedule) => schedule.ReverseAdd(value);

    public static Schedule operator +(uint value, Schedule schedule) => schedule.ReverseAdd(value);

    public static Schedule operator +(long value, Schedule schedule) => schedule.ReverseAdd(value);

    public static Schedule operator +(ulong value, Schedule schedule) => schedule.ReverseAdd(value);

    public static Schedule operator +(BigInteger value, Schedule schedule) => schedule.ReverseAdd(value);

    public static Schedule operator +(bool value, Schedule schedule) => schedule.ReverseAdd(value);

    public static Schedule operator +(char value, Schedule schedule) => schedule.ReverseAdd(value);

    public static Schedule operator -(Schedule left, Schedule right)
    {
        return left.Subtract(right);
    }

    public static Schedule operator -(Schedule schedule, double value)
    {
        return schedule.Subtract(value);
    }

    public static Schedule operator -(Schedule schedule, int value) => schedule.Subtract(value);

    public static Schedule operator -(Schedule schedule, uint value) => schedule.Subtract(value);

    public static Schedule operator -(Schedule schedule, long value) => schedule.Subtract(value);

    public static Schedule operator -(Schedule schedule, ulong value) => schedule.Subtract(value);

    public static Schedule operator -(Schedule schedule, BigInteger value) => schedule.Subtract(value);

    public static Schedule operator -(Schedule schedule, bool value) => schedule.Subtract(value);

    public static Schedule operator -(Schedule schedule, char value) => schedule.Subtract(value);

    public static Schedule operator -(double value, Schedule schedule)
    {
        return schedule.ReverseSubtract(value);
    }

    public static Schedule operator -(int value, Schedule schedule) => schedule.ReverseSubtract(value);

    public static Schedule operator -(uint value, Schedule schedule) => schedule.ReverseSubtract(value);

    public static Schedule operator -(long value, Schedule schedule) => schedule.ReverseSubtract(value);

    public static Schedule operator -(ulong value, Schedule schedule) => schedule.ReverseSubtract(value);

    public static Schedule operator -(BigInteger value, Schedule schedule) => schedule.ReverseSubtract(value);

    public static Schedule operator -(bool value, Schedule schedule) => schedule.ReverseSubtract(value);

    public static Schedule operator -(char value, Schedule schedule) => schedule.ReverseSubtract(value);

    public static Schedule operator &(Schedule left, Schedule right)
    {
        return left.LogicalAnd(right);
    }

    public static Schedule operator |(Schedule left, Schedule right)
    {
        return left.LogicalOr(right);
    }

    public static Schedule operator !(Schedule schedule)
    {
        return schedule.Invert();
    }

    public bool Equals(Schedule? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(Name, other.Name)
            && Type == other.Type
            && RuleSets.SequenceEqual(other.RuleSets);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Schedule);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(Name);
            hash = (hash * 397) ^ Type.GetHashCode();
            foreach (RuleSet ruleSet in RuleSets)
            {
                hash = (hash * 397) ^ ruleSet.GetHashCode();
            }

            return hash;
        }
    }

    public IEnumerator<RuleSet> GetEnumerator()
    {
        return RuleSets.GetEnumerator();
    }

    private Schedule Combine(
        Schedule other,
        Func<RuleSet, RuleSet, RuleSet> operation,
        string operationName,
        string? name)
    {
        DomainGuard.NotNull(other, nameof(other));
        (IReadOnlyList<SchedulePeriod> left, IReadOnlyList<SchedulePeriod> right) =
            UnifyCompactizedSchedules(Compactize(), other.Compactize());
        return FromCompact(
            name ?? $"{Name}:{operationName}:{other.Name}",
            Enumerable.Range(0, left.Count).Select(index => new SchedulePeriod(
                left[index].Start,
                left[index].End,
                operation(left[index].RuleSet, right[index].RuleSet))));
    }

    private Schedule MapCompact(Func<RuleSet, RuleSet> operation, string name)
    {
        return FromCompact(
            name,
            Compactize().Select(period => new SchedulePeriod(
                period.Start,
                period.End,
                operation(period.RuleSet))));
    }

    private Schedule MultiplyPythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "multiplication");
        return MapCompact(
            ruleSet => ruleSet.Multiply(value),
            name ?? $"{Name}:MUL:{scalarName}");
    }

    private Schedule AddPythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "addition");
        return MapCompact(
            ruleSet => ruleSet.Add(value),
            name ?? $"{Name}:ADD:{scalarName}");
    }

    private Schedule ReverseAddPythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "reverse addition");
        return MapCompact(
            ruleSet => ruleSet.ReverseAdd(value),
            name ?? $"{scalarName}:ADD:{Name}");
    }

    private Schedule SubtractPythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "subtraction");
        return MapCompact(
            ruleSet => ruleSet.Subtract(value),
            name ?? $"{Name}:SUB:{scalarName}");
    }

    private Schedule ReverseSubtractPythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "reverse subtraction");
        return MapCompact(
            ruleSet => ruleSet.ReverseSubtract(value),
            name ?? $"{scalarName}:SUB:{Name}");
    }

    private Schedule DividePythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "division");
        return MapCompact(
            ruleSet => ruleSet.Divide(value),
            name ?? $"{Name}:DIV:{scalarName}");
    }

    private Schedule ReverseDividePythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "reverse division");
        return MapCompact(
            ruleSet => ruleSet.ReverseDivide(value),
            name ?? $"{scalarName}:DIV:{Name}");
    }

    private Schedule ElementEqualPythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "equality comparison");
        return MapCompact(
            ruleSet => ruleSet.ElementEqual(value),
            name ?? $"{Name}:EQ:{scalarName}");
    }

    private Schedule ElementNotEqualPythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "inequality comparison");
        return MapCompact(
            ruleSet => ruleSet.ElementNotEqual(value),
            name ?? $"{Name}:NE:{scalarName}");
    }

    private Schedule LessThanPythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "less-than comparison");
        return MapCompact(
            ruleSet => ruleSet.LessThan(value),
            name ?? $"{Name}:LT:{scalarName}");
    }

    private Schedule LessThanOrEqualPythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "less-than-or-equal comparison");
        return MapCompact(
            ruleSet => ruleSet.LessThanOrEqual(value),
            name ?? $"{Name}:LE:{scalarName}");
    }

    private Schedule GreaterThanPythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "greater-than comparison");
        return MapCompact(
            ruleSet => ruleSet.GreaterThan(value),
            name ?? $"{Name}:GT:{scalarName}");
    }

    private Schedule GreaterThanOrEqualPythonScalar<T>(T value, string? name)
    {
        string scalarName = DaySchedule.FormatPythonScalar(value, "greater-than-or-equal comparison");
        return MapCompact(
            ruleSet => ruleSet.GreaterThanOrEqual(value),
            name ?? $"{Name}:GE:{scalarName}");
    }

    private static Schedule FromConstantObject(
        string? name,
        object value,
        ScheduleType? type,
        string parameterName)
    {
        string scheduleName = NormalizeScheduleName(name);
        if (value is RuleSet ruleSet)
        {
            return FromConstant(scheduleName, ruleSet, type ?? ruleSet.Type);
        }

        if (value is DaySchedule daySchedule)
        {
            return FromConstant(scheduleName, daySchedule, type ?? daySchedule.Type);
        }

        ScheduleType resultType = type ?? ScheduleType.Real;
        string ruleSetName = $"{scheduleName}:ruleset";
        RuleSet scalarRuleSet = CreateScalarRuleSetFromObject(
            ruleSetName,
            value,
            resultType,
            parameterName);
        return new Schedule(
            scheduleName,
            Enumerable.Repeat(scalarRuleSet, FixedLength),
            resultType);
    }

    private static string NormalizeScheduleName(string? name)
    {
        return name is null
            ? "anonymous"
            : DomainGuard.RequiredText(name, nameof(name));
    }

    private static RuleSet CoerceRuleSet(
        object value,
        ScheduleType type,
        string name,
        string parameterName)
    {
        if (value is RuleSet ruleSet)
        {
            if (ruleSet.Type != type)
            {
                throw new ArgumentException(
                    $"RuleSet type mismatch: expected {type.CanonicalName()}, got {ruleSet.Type.CanonicalName()}.",
                    parameterName);
            }

            return ruleSet;
        }

        if (value is DaySchedule daySchedule)
        {
            if (daySchedule.Type != type)
            {
                throw new ArgumentException(
                    $"DaySchedule type mismatch: expected {type.CanonicalName()}, got {daySchedule.Type.CanonicalName()}.",
                    parameterName);
            }

            return RuleSet.FromDaySchedule(name, daySchedule);
        }

        return CreateScalarRuleSetFromObject(name, value, type, parameterName);
    }

    private static RuleSet CreateScalarRuleSetFromObject(
        string name,
        object value,
        ScheduleType type,
        string parameterName)
    {
        return value switch
        {
            bool scalar => CreateScalarRuleSet(name, scalar, type),
            sbyte scalar => CreateScalarRuleSet(name, scalar, type),
            byte scalar => CreateScalarRuleSet(name, scalar, type),
            short scalar => CreateScalarRuleSet(name, scalar, type),
            ushort scalar => CreateScalarRuleSet(name, scalar, type),
            int scalar => CreateScalarRuleSet(name, scalar, type),
            uint scalar => CreateScalarRuleSet(name, scalar, type),
            long scalar => CreateScalarRuleSet(name, scalar, type),
            ulong scalar => CreateScalarRuleSet(name, scalar, type),
            BigInteger scalar => CreateScalarRuleSet(name, scalar, type),
            float scalar => CreateScalarRuleSet(name, scalar, type),
            double scalar => CreateScalarRuleSet(name, scalar, type),
            _ => throw new ArgumentException(
                "A schedule value must be a Python-compatible bool, integer, float, DaySchedule, or RuleSet.",
                parameterName),
        };
    }

    private static RuleSet CreateScalarRuleSet<T>(
        string name,
        T value,
        ScheduleType type)
    {
        DaySchedule weekdays = DaySchedule.ConstantFromPythonScalar(
            $"{name}:day",
            value,
            type);
        DaySchedule weekends = DaySchedule.ConstantFromPythonScalar(
            $"{name}:day",
            value,
            type);
        return new RuleSet(name, weekdays, weekends, type: type);
    }

    private static RuleSet CreateZeroRuleSet(
        string scheduleName,
        int index,
        ScheduleType type)
    {
        return CreateScalarRuleSet(
            $"{scheduleName}:default:{index + 1:D3}",
            0,
            type);
    }

    private static RuleSet DeepCopyRuleSet(RuleSet source)
    {
        return new RuleSet(
            $"{source.Name}:COPY",
            DeepCopyDaySchedule(source.Weekdays),
            DeepCopyDaySchedule(source.Weekends),
            source.Monday is null ? null : DeepCopyDaySchedule(source.Monday),
            source.Tuesday is null ? null : DeepCopyDaySchedule(source.Tuesday),
            source.Wednesday is null ? null : DeepCopyDaySchedule(source.Wednesday),
            source.Thursday is null ? null : DeepCopyDaySchedule(source.Thursday),
            source.Friday is null ? null : DeepCopyDaySchedule(source.Friday),
            source.Saturday is null ? null : DeepCopyDaySchedule(source.Saturday),
            source.Sunday is null ? null : DeepCopyDaySchedule(source.Sunday),
            source.Holiday is null ? null : DeepCopyDaySchedule(source.Holiday),
            source.Type);
    }

    private static DaySchedule DeepCopyDaySchedule(DaySchedule source)
    {
        return new DaySchedule(
            $"{source.Name}:COPY",
            source.Values,
            source.Type,
            source.Unit);
    }

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

    private static void AddRuleSetIdfFields(List<string?> fields, RuleSet ruleSet)
    {
        (string Name, DaySchedule? Override)[] weekdays =
        {
            ("Monday", ruleSet.Monday),
            ("Tuesday", ruleSet.Tuesday),
            ("Wednesday", ruleSet.Wednesday),
            ("Thursday", ruleSet.Thursday),
            ("Friday", ruleSet.Friday),
        };
        if (weekdays.Any(item => item.Override is not null))
        {
            foreach ((string selection, DaySchedule? dayOverride) in weekdays)
            {
                AddDayIdfFields(fields, selection, dayOverride ?? ruleSet.Weekdays);
            }
        }
        else
        {
            AddDayIdfFields(fields, "Weekdays", ruleSet.Weekdays);
        }

        (string Name, DaySchedule? Override)[] weekends =
        {
            ("Saturday", ruleSet.Saturday),
            ("Sunday", ruleSet.Sunday),
        };
        if (weekends.Any(item => item.Override is not null))
        {
            foreach ((string selection, DaySchedule? dayOverride) in weekends)
            {
                AddDayIdfFields(fields, selection, dayOverride ?? ruleSet.Weekends);
            }
        }
        else
        {
            AddDayIdfFields(fields, "Weekends", ruleSet.Weekends);
        }

        if (ruleSet.Holiday is not null)
        {
            AddDayIdfFields(fields, "Holiday", ruleSet.Holiday);
        }

        AddDayIdfFields(fields, "AllOtherDays", ruleSet.Weekends);
    }

    private static void AddDayIdfFields(
        List<string?> fields,
        string selection,
        DaySchedule daySchedule)
    {
        fields.Add($"For: {selection}");
        foreach (DayScheduleSegment segment in daySchedule.Compactize())
        {
            string until = segment.Until == TimeSpan.FromHours(24)
                ? "24:00"
                : $"{(int)segment.Until.TotalHours:00}:{segment.Until.Minutes:00}";
            fields.Add($"Until: {until}");
            fields.Add(daySchedule.Type == ScheduleType.OnOff
                ? ((int)segment.Value).ToString(CultureInfo.InvariantCulture)
                : DaySchedule.FormatPythonScalar(segment.Value, "IDF serialization"));
        }
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

    private static RuleSet FindRuleSet(IReadOnlyList<SchedulePeriod> schedule, DateTime date)
    {
        foreach (SchedulePeriod period in schedule)
        {
            if (period.Start <= date && date <= period.End)
            {
                return period.RuleSet;
            }
        }

        throw new ArgumentException(
            $"A compactized schedule does not define a RuleSet for {date:MM-dd}.",
            nameof(schedule));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal static DateTime ParseDate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A schedule date is required.", parameterName);
        }

        if (Regex.IsMatch(value, @"^\d{8}$", RegexOptions.CultureInvariant))
        {
            return new DateTime(
                ParseDigits(value, 0, 4),
                ParseDigits(value, 4, 2),
                ParseDigits(value, 6, 2));
        }

        if (Regex.IsMatch(value, @"^\d{4}$", RegexOptions.CultureInvariant))
        {
            return new DateTime(
                DefaultYear,
                ParseDigits(value, 0, 2),
                ParseDigits(value, 2, 2));
        }

        int[] parts = Regex.Matches(value, @"\d+", RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture))
            .ToArray();
        if (parts.Length == 2)
        {
            return new DateTime(DefaultYear, parts[0], parts[1]);
        }

        if (parts.Length == 3)
        {
            return new DateTime(parts[0], parts[1], parts[2]);
        }

        throw new ArgumentException(
            $"The schedule date {PythonRepr(value)} is not supported.",
            parameterName);
    }

    private static int ParseDigits(string value, int start, int length)
    {
        int result = 0;
        for (int index = start; index < start + length; index++)
        {
            result = checked((result * 10) + (value[index] - '0'));
        }

        return result;
    }

    internal static DateTime NormalizeDate(DateTime date)
    {
        if (date.Month == 2 && date.Day == 29)
        {
            throw new ArgumentOutOfRangeException(nameof(date), date, "The fixed annual schedule does not represent leap day.");
        }

        return new DateTime(DefaultYear, date.Month, date.Day);
    }
}
