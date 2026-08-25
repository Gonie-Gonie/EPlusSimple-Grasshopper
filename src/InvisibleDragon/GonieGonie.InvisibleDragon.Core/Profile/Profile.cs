using System.Collections.ObjectModel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Profile;

/// <summary>
/// A named collection of annual schedules used by a thermal zone.
/// </summary>
public sealed record Profile
{
    public Profile(
        EntityId id,
        string name,
        Schedule? heatingSetpoint = null,
        Schedule? coolingSetpoint = null,
        Schedule? hvacAvailability = null,
        Schedule? occupant = null,
        Schedule? lighting = null,
        Schedule? equipment = null,
        Schedule? hotWater = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = DomainGuard.RequiredText(name, nameof(name));
        RequireType(heatingSetpoint, ScheduleType.Temperature, nameof(heatingSetpoint));
        RequireType(coolingSetpoint, ScheduleType.Temperature, nameof(coolingSetpoint));
        RequireType(hvacAvailability, ScheduleType.OnOff, nameof(hvacAvailability));
        RequireLightingType(lighting, nameof(lighting));
        RequireType(occupant, ScheduleType.Real, nameof(occupant));
        RequireType(equipment, ScheduleType.Real, nameof(equipment));
        RequireType(hotWater, ScheduleType.Real, nameof(hotWater));

        HeatingSetpoint = heatingSetpoint;
        CoolingSetpoint = coolingSetpoint;
        HvacAvailability = hvacAvailability;
        Occupant = occupant;
        Lighting = lighting;
        Equipment = equipment;
        HotWater = hotWater;
    }

    public EntityId Id { get; }

    public string Name { get; }

    public Schedule? HeatingSetpoint { get; }

    public Schedule? CoolingSetpoint { get; }

    public Schedule? HvacAvailability { get; }

    public Schedule? Occupant { get; }

    public Schedule? Lighting { get; }

    public Schedule? Equipment { get; }

    public Schedule? HotWater { get; }

    public IReadOnlyList<IdfObject> ToIdfObjects()
    {
        Schedule?[] schedules =
        {
            HeatingSetpoint,
            CoolingSetpoint,
            HvacAvailability,
            Occupant,
            Lighting,
            Equipment,
            HotWater,
        };
        var objects = new List<IdfObject>(schedules.Length);
        foreach (Schedule? schedule in schedules)
        {
            if (schedule is not null)
            {
                objects.Add(schedule.ToIdfObject());
            }
        }

        return new ReadOnlyCollection<IdfObject>(objects);
    }

    public ValidationResult Validate()
    {
        List<Diagnostic> diagnostics = new();
        if (HeatingSetpoint is not null
            && CoolingSetpoint is not null
            && HeatingSetpoint.Maximum > CoolingSetpoint.Minimum)
        {
            diagnostics.Add(new Diagnostic(
                "INVISIBLEDRAGON.PROFILE.SETPOINT_OVERLAP",
                DiagnosticSeverity.Warning,
                "The heating setpoint can exceed the cooling setpoint.",
                Id,
                suggestedAction: "Review the setpoint schedules and maintain an appropriate deadband."));
        }

        return diagnostics.Count == 0 ? ValidationResult.Success : ValidationResult.From(diagnostics);
    }

    private static void RequireType(Schedule? schedule, ScheduleType expected, string parameterName)
    {
        if (schedule is not null && schedule.Type != expected)
        {
            throw new ArgumentException(
                $"Schedule '{schedule.Name}' must have {expected} type, not {schedule.Type}.",
                parameterName);
        }
    }

    private static void RequireLightingType(Schedule? schedule, string parameterName)
    {
        if (schedule is not null
            && schedule.Type is not ScheduleType.OnOff
            && schedule.Type is not ScheduleType.Fraction)
        {
            throw new ArgumentException(
                $"Schedule '{schedule.Name}' must have OnOff or Fraction type, not {schedule.Type}.",
                parameterName);
        }
    }
}
