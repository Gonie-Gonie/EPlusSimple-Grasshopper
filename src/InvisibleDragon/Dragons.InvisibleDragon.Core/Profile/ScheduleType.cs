using System.Globalization;
using Dragons.InvisibleDragon.Idf;

namespace Dragons.InvisibleDragon.Profile;

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
    /// <summary>
    /// Gets the canonical lower-case name used by the pinned Python API.
    /// </summary>
    public static string CanonicalName(this ScheduleType type)
    {
        return type switch
        {
            ScheduleType.Temperature => "temperature",
            ScheduleType.OnOff => "onoff",
            ScheduleType.Fraction => "fraction",
            ScheduleType.Real => "real",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown schedule type."),
        };
    }

    /// <summary>
    /// Gets the exact ScheduleTypeLimits object name used by the pinned Python API.
    /// </summary>
    public static string IdfObjectName(this ScheduleType type)
    {
        return type switch
        {
            ScheduleType.Temperature => "ScheduleTypeLimits:Temperature",
            ScheduleType.OnOff => "ScheduleTypeLimits:Onoff",
            ScheduleType.Fraction => "ScheduleTypeLimits:Fraction",
            ScheduleType.Real => "ScheduleTypeLimits:Real",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown schedule type."),
        };
    }

    /// <summary>
    /// Gets the EnergyPlus numeric type for this schedule value domain.
    /// </summary>
    public static string NumericType(this ScheduleType type)
    {
        return type switch
        {
            ScheduleType.OnOff => "Discrete",
            ScheduleType.Temperature or ScheduleType.Fraction or ScheduleType.Real => "Continuous",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown schedule type."),
        };
    }

    /// <summary>
    /// Gets the EnergyPlus unit type for this schedule value domain.
    /// </summary>
    public static string UnitType(this ScheduleType type)
    {
        return type switch
        {
            ScheduleType.Temperature => "Temperature",
            ScheduleType.OnOff or ScheduleType.Fraction or ScheduleType.Real => "Dimensionless",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown schedule type."),
        };
    }

    /// <summary>
    /// Creates the exact five-field ScheduleTypeLimits object used by the pinned Python API.
    /// </summary>
    public static IdfObject ToIdfObject(this ScheduleType type)
    {
        string? lowerLimit = FormatLimit(type.LowerLimit());
        string? upperLimit = FormatLimit(type.UpperLimit());
        return new IdfObject(
            "ScheduleTypeLimits",
            new[]
            {
                type.IdfObjectName(),
                lowerLimit,
                upperLimit,
                type.NumericType(),
                type.UnitType(),
            });
    }

    /// <summary>
    /// Validates numeric and Boolean values using the pinned Python value-domain rules.
    /// CLR numeric results are normalized to <see cref="double"/>.
    /// </summary>
    public static double ValidateValue(this ScheduleType type, object? value, string parameterName = "value")
    {
        double numericValue = value switch
        {
            bool flag => flag ? 1d : 0d,
            byte number => number,
            sbyte number => number,
            short number => number,
            ushort number => number,
            int number => number,
            uint number => number,
            long number => number,
            ulong number => number,
            float number => number,
            double number => number,
            decimal number => (double)number,
            _ => throw new ArgumentException(
                $"A {type.CanonicalName()} schedule value must be numeric or Boolean.",
                parameterName),
        };

        return type.ValidateValue(numericValue, parameterName);
    }

    /// <summary>
    /// Validates one normalized CLR schedule value.
    /// </summary>
    /// <remarks>
    /// Non-finite values are rejected for every schedule type because EnergyPlus IDF
    /// numeric fields must remain finite. The pinned Python REAL branch accepts them;
    /// that deliberate safety divergence requires a compatibility exception rather
    /// than weakening this engineering validation boundary.
    /// </remarks>
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
            case ScheduleType.OnOff:
                return value == 0 ? 0d : 1d;
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

    private static string? FormatLimit(double? value)
    {
        return value?.ToString("R", CultureInfo.InvariantCulture);
    }
}

public sealed class ScheduleOperationException : InvalidOperationException
{
    public ScheduleOperationException(string message)
        : base(message)
    {
    }
}
