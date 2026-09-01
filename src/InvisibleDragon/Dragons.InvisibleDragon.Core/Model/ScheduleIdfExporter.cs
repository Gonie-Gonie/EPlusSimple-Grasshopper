using System.Globalization;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Profile;

namespace Dragons.InvisibleDragon.Model;

internal static class ScheduleIdfExporter
{
    internal static string TypeLimitName(ScheduleType type, bool legacySimpleDragon = false)
    {
        if (legacySimpleDragon)
        {
            return type.IdfObjectName();
        }

        string typeName = type switch
        {
            ScheduleType.Temperature => nameof(ScheduleType.Temperature),
            ScheduleType.OnOff => nameof(ScheduleType.OnOff),
            ScheduleType.Fraction => nameof(ScheduleType.Fraction),
            ScheduleType.Real => nameof(ScheduleType.Real),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown schedule type."),
        };
        return $"ScheduleTypeLimits:{typeName}";
    }

    internal static IEnumerable<IdfObject> CreateTypeLimits(
        IdfGenerationContext context,
        bool legacySimpleDragon = false)
    {
        ScheduleType[] types =
        {
            ScheduleType.Temperature,
            ScheduleType.OnOff,
            ScheduleType.Fraction,
            ScheduleType.Real,
        };
        foreach (ScheduleType type in types)
        {
            yield return context.CreateRaw(
                "ScheduleTypeLimits",
                TypeLimitName(type, legacySimpleDragon),
                type.LowerLimit(),
                type.UpperLimit(),
                type.NumericType(),
                type.UnitType());
        }
    }

    internal static IdfObject Create(
        IdfGenerationContext context,
        Schedule schedule,
        bool legacySimpleDragon = false)
    {
        List<object?> fields = new()
        {
            schedule.Name,
            TypeLimitName(schedule.Type, legacySimpleDragon),
        };
        foreach (SchedulePeriod period in schedule.Compactize())
        {
            fields.Add(
                "Through: "
                + period.End.Month.ToString(CultureInfo.InvariantCulture)
                + "/"
                + period.End.Day.ToString(CultureInfo.InvariantCulture));
            if (legacySimpleDragon)
            {
                AddLegacyRuleSet(fields, period.RuleSet);
            }
            else
            {
                AddDay(fields, "Monday", period.RuleSet.GetDaySchedule(DayOfWeek.Monday));
                AddDay(fields, "Tuesday", period.RuleSet.GetDaySchedule(DayOfWeek.Tuesday));
                AddDay(fields, "Wednesday", period.RuleSet.GetDaySchedule(DayOfWeek.Wednesday));
                AddDay(fields, "Thursday", period.RuleSet.GetDaySchedule(DayOfWeek.Thursday));
                AddDay(fields, "Friday", period.RuleSet.GetDaySchedule(DayOfWeek.Friday));
                AddDay(fields, "Saturday", period.RuleSet.GetDaySchedule(DayOfWeek.Saturday));
                AddDay(fields, "Sunday", period.RuleSet.GetDaySchedule(DayOfWeek.Sunday));
                AddDay(fields, "Holiday SummerDesignDay WinterDesignDay CustomDay1 CustomDay2", period.RuleSet.GetDaySchedule(DayOfWeek.Sunday, true));
            }
        }

        return context.CreateRaw("Schedule:Compact", fields.ToArray());
    }

    private static void AddLegacyRuleSet(List<object?> fields, RuleSet ruleSet)
    {
        (string Name, DaySchedule? Override)[] weekdays =
        {
            ("Monday", ruleSet.Monday),
            ("Tuesday", ruleSet.Tuesday),
            ("Wednesday", ruleSet.Wednesday),
            ("Thursday", ruleSet.Thursday),
            ("Friday", ruleSet.Friday),
        };
        if (weekdays.Any(item => IsDistinctOverride(item.Override, ruleSet.Weekdays)))
        {
            foreach ((string name, DaySchedule? dayOverride) in weekdays)
            {
                AddLegacyDay(
                    fields,
                    name,
                    IsDistinctOverride(dayOverride, ruleSet.Weekdays)
                        ? dayOverride!
                        : ruleSet.Weekdays,
                    ruleSet.Type);
            }
        }
        else
        {
            AddLegacyDay(fields, "Weekdays", ruleSet.Weekdays, ruleSet.Type);
        }

        (string Name, DaySchedule? Override)[] weekends =
        {
            ("Saturday", ruleSet.Saturday),
            ("Sunday", ruleSet.Sunday),
        };
        if (weekends.Any(item => IsDistinctOverride(item.Override, ruleSet.Weekends)))
        {
            foreach ((string name, DaySchedule? dayOverride) in weekends)
            {
                AddLegacyDay(
                    fields,
                    name,
                    IsDistinctOverride(dayOverride, ruleSet.Weekends)
                        ? dayOverride!
                        : ruleSet.Weekends,
                    ruleSet.Type);
            }
        }
        else
        {
            AddLegacyDay(fields, "Weekends", ruleSet.Weekends, ruleSet.Type);
        }

        if (IsDistinctOverride(ruleSet.Holiday, ruleSet.Weekends))
        {
            AddLegacyDay(fields, "Holiday", ruleSet.Holiday!, ruleSet.Type);
        }

        AddLegacyDay(fields, "AllOtherDays", ruleSet.Weekends, ruleSet.Type);
    }

    private static bool IsDistinctOverride(DaySchedule? candidate, DaySchedule fallback)
    {
        return candidate is not null && !candidate.Equals(fallback);
    }

    private static void AddLegacyDay(
        List<object?> fields,
        string selection,
        DaySchedule day,
        ScheduleType type)
    {
        fields.Add($"For: {selection}");
        foreach (DayScheduleSegment segment in day.Compactize())
        {
            string until = segment.Until == TimeSpan.FromHours(24)
                ? "24:00"
                : $"{(int)segment.Until.TotalHours:00}:{segment.Until.Minutes:00}";
            fields.Add($"Until: {until}");
            fields.Add(FormatLegacyValue(segment.Value, type));
        }
    }

    private static string FormatLegacyValue(double value, ScheduleType type)
    {
        string result = value.ToString("R", CultureInfo.InvariantCulture);
        if (type != ScheduleType.OnOff
            && result.IndexOf('.') < 0
            && result.IndexOf('E') < 0
            && result.IndexOf('e') < 0)
        {
            result += ".0";
        }

        return result;
    }

    private static void AddDay(List<object?> fields, string selection, DaySchedule day)
    {
        fields.Add($"For: {selection}");
        foreach (DayScheduleSegment segment in day.Compactize())
        {
            string until = segment.Until == TimeSpan.FromHours(24)
                ? "24:00"
                : $"{(int)segment.Until.TotalHours:00}:{segment.Until.Minutes:00}";
            fields.Add($"Until: {until}");
            fields.Add(segment.Value);
        }
    }
}
