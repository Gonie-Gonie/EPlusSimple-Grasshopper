namespace GonieGonie.InvisibleDragon.Profile;

/// <summary>
/// Value domains supported by EnergyPlus schedules.
/// </summary>
public enum ScheduleType
{
    Temperature,
    OnOff,
    Fraction,
    Real,
}

public static class ScheduleTypeExtensions
{
    public static double ValidateValue(this ScheduleType type, double value, string parameterName = "value")
    {
        if (!Enum.IsDefined(typeof(ScheduleType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown schedule type.");
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A schedule value must be finite.");
        }

        switch (type)
        {
            case ScheduleType.Temperature when value < -50 || value > 200:
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A temperature schedule value must be in [-50, 200] degrees Celsius.");
            case ScheduleType.OnOff when value != 0 && value != 1:
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "An on/off schedule value must be exactly zero or one.");
            case ScheduleType.Fraction when value < 0 || value > 1:
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A fraction schedule value must be in [0, 1].");
        }

        return value;
    }

    public static double? LowerLimit(this ScheduleType type)
    {
        return type switch
        {
            ScheduleType.Temperature => -50,
            ScheduleType.OnOff => 0,
            ScheduleType.Fraction => 0,
            ScheduleType.Real => null,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown schedule type."),
        };
    }

    public static double? UpperLimit(this ScheduleType type)
    {
        return type switch
        {
            ScheduleType.Temperature => 200,
            ScheduleType.OnOff => 1,
            ScheduleType.Fraction => 1,
            ScheduleType.Real => null,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown schedule type."),
        };
    }
}

public sealed class ScheduleOperationException : InvalidOperationException
{
    public ScheduleOperationException(string message)
        : base(message)
    {
    }
}
