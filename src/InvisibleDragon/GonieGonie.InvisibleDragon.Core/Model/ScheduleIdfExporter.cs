using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Profile;

namespace GonieGonie.InvisibleDragon.Model;

internal static class ScheduleIdfExporter
{
    internal static string TypeLimitName(ScheduleType type) => $"ScheduleTypeLimits:{type}";

    internal static IEnumerable<IdfObject> CreateTypeLimits(IdfGenerationContext context)
    {
        yield return context.CreateRaw("ScheduleTypeLimits", TypeLimitName(ScheduleType.Temperature), -50, 200, "Continuous", "Temperature");
        yield return context.CreateRaw("ScheduleTypeLimits", TypeLimitName(ScheduleType.OnOff), 0, 1, "Discrete", "Dimensionless");
        yield return context.CreateRaw("ScheduleTypeLimits", TypeLimitName(ScheduleType.Fraction), 0, 1, "Continuous", "Dimensionless");
        yield return context.CreateRaw("ScheduleTypeLimits", TypeLimitName(ScheduleType.Real), null, null, "Continuous", "Dimensionless");
    }

    internal static IdfObject Create(IdfGenerationContext context, Schedule schedule)
    {
        List<object?> fields = new() { schedule.Name, TypeLimitName(schedule.Type) };
        foreach (SchedulePeriod period in schedule.Compactize())
        {
            fields.Add($"Through: {period.End:M/d}");
            AddDay(fields, "Monday", period.RuleSet.GetDaySchedule(DayOfWeek.Monday));
            AddDay(fields, "Tuesday", period.RuleSet.GetDaySchedule(DayOfWeek.Tuesday));
            AddDay(fields, "Wednesday", period.RuleSet.GetDaySchedule(DayOfWeek.Wednesday));
            AddDay(fields, "Thursday", period.RuleSet.GetDaySchedule(DayOfWeek.Thursday));
            AddDay(fields, "Friday", period.RuleSet.GetDaySchedule(DayOfWeek.Friday));
            AddDay(fields, "Saturday", period.RuleSet.GetDaySchedule(DayOfWeek.Saturday));
            AddDay(fields, "Sunday", period.RuleSet.GetDaySchedule(DayOfWeek.Sunday));
            AddDay(fields, "Holiday SummerDesignDay WinterDesignDay CustomDay1 CustomDay2", period.RuleSet.GetDaySchedule(DayOfWeek.Sunday, true));
        }

        return context.CreateRaw("Schedule:Compact", fields.ToArray());
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
