using System.Collections;
using System.Collections.ObjectModel;
using System.Numerics;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Profile;

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

    public DateTime Start { get; }

    public DateTime End { get; }

    public RuleSet RuleSet { get; }
}

/// <summary>
/// An immutable non-leap-year schedule with one rule set for every day of 2026.
/// </summary>
public sealed class Schedule : IReadOnlyList<RuleSet>, IEquatable<Schedule>
{
    public const int DefaultYear = 2026;

    public const int FixedLength = 365;

    private static readonly DateTime FirstDay = new(DefaultYear, 1, 1);

    public Schedule(string name, IEnumerable<RuleSet> ruleSets, ScheduleType? type = null)
    {
        Name = DomainGuard.RequiredText(name, nameof(name));
        DomainGuard.NotNull(ruleSets, nameof(ruleSets));
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

    public int Count => FixedLength;

    public RuleSet this[int index] => RuleSets[index];

    public RuleSet this[DateTime date] => RuleSets[(NormalizeDate(date) - FirstDay).Days];

    public double Minimum => RuleSets.Min(ruleSet => ruleSet.Minimum);

    public double Maximum => RuleSets.Max(ruleSet => ruleSet.Maximum);

    public double IntegralHours => Enumerable.Range(0, FixedLength)
        .Sum(index => RuleSets[index].GetDaySchedule(FirstDay.AddDays(index).DayOfWeek).IntegralHours);

    // Preserve the pinned Python 0.7.0 contract: this is the mean daily
    // integral, despite the historical property name suggesting a value mean.
    public double Average => IntegralHours / FixedLength;

    public double PositiveAverage
    {
        get
        {
            double total = 0;
            int count = 0;
            for (int index = 0; index < FixedLength; index++)
            {
                DaySchedule daySchedule = RuleSets[index].GetDaySchedule(FirstDay.AddDays(index).DayOfWeek);
                foreach (double value in daySchedule)
                {
                    if (value > 0)
                    {
                        total += value;
                        count++;
                    }
                }
            }

            return count == 0 ? 0 : total / count;
        }
    }

    public static Schedule Constant(string name, double value, ScheduleType type = ScheduleType.Real)
    {
        RuleSet ruleSet = RuleSet.Constant($"{name}:ruleset", value, type);
        return new Schedule(name, Enumerable.Repeat(ruleSet, FixedLength), type);
    }

    public static Schedule Constant<T>(string name, T value, ScheduleType type = ScheduleType.Real)
    {
        RuleSet ruleSet = RuleSet.Constant($"{name}:ruleset", value, type);
        return new Schedule(name, Enumerable.Repeat(ruleSet, FixedLength), type);
    }

    public static Schedule FromCompact(string name, IEnumerable<SchedulePeriod> periods)
    {
        DomainGuard.NotNull(periods, nameof(periods));
        SchedulePeriod[] copy = periods.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("At least one annual schedule period is required.", nameof(periods));
        }

        ScheduleType type = copy[0].RuleSet.Type;
        if (copy.Any(period => period is null || period.RuleSet.Type != type))
        {
            throw new ArgumentException("Annual schedule periods cannot be null or mix schedule types.", nameof(periods));
        }

        RuleSet zero = RuleSet.Constant($"{name}:default", 0, type);
        RuleSet[] values = Enumerable.Repeat(zero, FixedLength).ToArray();
        foreach (SchedulePeriod period in copy)
        {
            int start = (period.Start - FirstDay).Days;
            int end = (period.End - FirstDay).Days;
            for (int index = start; index <= end; index++)
            {
                values[index] = period.RuleSet;
            }
        }

        return new Schedule(name, values, type);
    }

    public static Schedule FromWindows(
        string name,
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

        return new Schedule(name, values, defaultRuleSet.Type);
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

    public Schedule AsType(ScheduleType type)
    {
        return MapCompact(ruleSet => ruleSet.AsType(type), Name);
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
            name ?? $"{Name}:CLIP");
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

    internal static DateTime NormalizeDate(DateTime date)
    {
        if (date.Month == 2 && date.Day == 29)
        {
            throw new ArgumentOutOfRangeException(nameof(date), date, "The fixed annual schedule does not represent leap day.");
        }

        return new DateTime(DefaultYear, date.Month, date.Day);
    }
}
