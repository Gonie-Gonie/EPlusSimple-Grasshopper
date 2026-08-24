using System.Collections;
using System.Collections.ObjectModel;
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

    public double Average => IntegralHours / (FixedLength * 24);

    public static Schedule Constant(string name, double value, ScheduleType type = ScheduleType.Real)
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
        bool[] assigned = new bool[FixedLength];
        foreach (SchedulePeriod period in copy)
        {
            int start = (period.Start - FirstDay).Days;
            int end = (period.End - FirstDay).Days;
            for (int index = start; index <= end; index++)
            {
                if (assigned[index])
                {
                    throw new ArgumentException($"Annual schedule periods overlap on {FirstDay.AddDays(index):MM-dd}.", nameof(periods));
                }

                assigned[index] = true;
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
            ScheduleWindow? match = copy.FirstOrDefault(window => window.Start <= date && date <= window.End);
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
        return new Schedule(Name, RuleSets.Select(ruleSet => ruleSet.AsType(type)), type);
    }

    public Schedule Scale(double factor, string? name = null)
    {
        return new Schedule(
            name ?? $"{Name}:MUL:{factor}",
            RuleSets.Select(ruleSet => ruleSet.Scale(factor)));
    }

    public Schedule Clip(double? minimum = null, double? maximum = null, string? name = null)
    {
        return new Schedule(
            name ?? $"{Name}:CLIP",
            RuleSets.Select(ruleSet => ruleSet.Clip(minimum, maximum)));
    }

    public IReadOnlyList<SchedulePeriod> Compactize()
    {
        List<SchedulePeriod> periods = new();
        for (int index = 0; index < FixedLength; index++)
        {
            DateTime date = FirstDay.AddDays(index);
            if (index == 0 || !RuleSets[index].Equals(RuleSets[index - 1]))
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
