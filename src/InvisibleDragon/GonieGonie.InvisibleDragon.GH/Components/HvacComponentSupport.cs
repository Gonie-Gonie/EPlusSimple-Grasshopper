using System.Globalization;
using Grasshopper.Kernel.Types;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Profile;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

internal static class HvacComponentSupport
{
    internal static TEnum EnumValue<TEnum>(int value, string inputName)
        where TEnum : struct
    {
        if (!typeof(TEnum).IsEnum || !Enum.IsDefined(typeof(TEnum), value))
        {
            string choices = string.Join(
                ", ",
                Enum.GetNames(typeof(TEnum)));
            throw new ArgumentException(
                $"{inputName} value '{value}' is invalid. Choose one of: {choices}.",
                inputName);
        }

        return (TEnum)Enum.ToObject(typeof(TEnum), value);
    }

    internal static double? OptionalPositive(double value, string inputName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(
                inputName,
                value,
                $"{inputName} must be 0 for autosizing or a positive SI value.");
        }

        return value == 0 ? null : value;
    }

    internal static SourceSystem Source(DragonSourceSystemGoo? goo, string inputName)
    {
        return goo?.Value
            ?? throw new ArgumentException($"{inputName} requires a non-empty source-system value.", inputName);
    }

    internal static TSource Source<TSource>(DragonSourceSystemGoo? goo, string inputName)
        where TSource : SourceSystem
    {
        SourceSystem source = Source(goo, inputName);
        return source as TSource
            ?? throw new ArgumentException(
                $"{inputName} requires {typeof(TSource).Name}, but '{source.Name}' is {source.GetType().Name}.",
                inputName);
    }

    internal static SupplySystem Supply(DragonSupplySystemGoo? goo, string inputName, int index)
    {
        return goo?.Value
            ?? throw new ArgumentException(
                $"{inputName} contains an empty supply-system value at index {index}.",
                inputName);
    }

    internal static T RequireObject<T>(object? value, string inputName)
        where T : class
    {
        object? candidate = value switch
        {
            GH_ObjectWrapper wrapper => wrapper.Value,
            IGH_Goo goo => goo.ScriptVariable(),
            _ => value,
        };
        return candidate as T
            ?? throw new ArgumentException(
                $"{inputName} requires a {typeof(T).Name} value.",
                inputName);
    }

    internal static Schedule?[] AvailabilitySchedules(
        IReadOnlyList<DragonScheduleGoo> goos,
        int systemCount)
    {
        if (goos.Count == 0)
        {
            return new Schedule?[systemCount];
        }

        Schedule[] schedules = goos.Select((goo, index) => goo?.Value
            ?? throw new ArgumentException(
                $"Availability Schedules contains an empty value at index {index}.",
                nameof(goos)))
            .ToArray();
        if (schedules.Any(schedule => schedule.Type != ScheduleType.OnOff))
        {
            throw new ArgumentException(
                "Availability Schedules accepts only OnOff schedules.",
                nameof(goos));
        }

        if (schedules.Length == 1)
        {
            return Enumerable.Repeat<Schedule?>(schedules[0], systemCount).ToArray();
        }

        if (schedules.Length != systemCount)
        {
            throw new ArgumentException(
                $"Availability Schedules must be empty, contain one broadcast schedule, or contain {systemCount} schedules.",
                nameof(goos));
        }

        return schedules.Cast<Schedule?>().ToArray();
    }

    internal static int[] AssignmentIndices(
        IReadOnlyList<int> suppliedIndices,
        int itemCount,
        int zoneCount,
        string inputName,
        bool broadcastSingleItemToAllZones)
    {
        if (itemCount == 0)
        {
            if (suppliedIndices.Count != 0)
            {
                throw new ArgumentException(
                    $"{inputName} cannot be supplied when its corresponding value list is empty.",
                    inputName);
            }

            return Array.Empty<int>();
        }

        int[] result;
        if (suppliedIndices.Count == 0)
        {
            if (zoneCount == 1)
            {
                result = Enumerable.Repeat(0, itemCount).ToArray();
            }
            else if (itemCount == zoneCount)
            {
                result = Enumerable.Range(0, zoneCount).ToArray();
            }
            else if (broadcastSingleItemToAllZones && itemCount == 1)
            {
                result = Enumerable.Range(0, zoneCount).ToArray();
            }
            else
            {
                throw new ArgumentException(
                    $"{inputName} is required because {itemCount} values cannot be mapped unambiguously to {zoneCount} zones.",
                    inputName);
            }
        }
        else if (suppliedIndices.Count == 1 && itemCount > 1)
        {
            result = Enumerable.Repeat(suppliedIndices[0], itemCount).ToArray();
        }
        else if (suppliedIndices.Count == itemCount)
        {
            result = suppliedIndices.ToArray();
        }
        else
        {
            throw new ArgumentException(
                $"{inputName} must be empty, contain one broadcast index, or contain {itemCount} indices.",
                inputName);
        }

        foreach (int index in result)
        {
            if (index < 0 || index >= zoneCount)
            {
                throw new ArgumentOutOfRangeException(
                    inputName,
                    index,
                    $"{inputName} uses zero-based zone indices from 0 through {Math.Max(zoneCount - 1, 0)}.");
            }
        }

        return result;
    }

    internal static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    internal static bool SourceDefinitionsEqual(SourceSystem left, SourceSystem right)
    {
        if (left.GetType() != right.GetType() ||
            !left.Id.Equals(right.Id) ||
            !string.Equals(left.Name, right.Name, StringComparison.Ordinal))
        {
            return false;
        }

        return left switch
        {
            HeatPump heatPump when right is HeatPump other =>
                heatPump.Fuel == other.Fuel &&
                heatPump.HeatingCoefficientOfPerformance == other.HeatingCoefficientOfPerformance &&
                heatPump.CoolingCoefficientOfPerformance == other.CoolingCoefficientOfPerformance &&
                heatPump.HeatingCapacityWatts == other.HeatingCapacityWatts &&
                heatPump.CoolingCapacityWatts == other.CoolingCapacityWatts,
            Boiler boiler when right is Boiler other =>
                boiler.Fuel == other.Fuel &&
                boiler.NominalThermalEfficiency == other.NominalThermalEfficiency &&
                boiler.NominalCapacityWatts == other.NominalCapacityWatts &&
                boiler.PumpMotorEfficiency == other.PumpMotorEfficiency &&
                boiler.SetpointTemperatureCelsius == other.SetpointTemperatureCelsius,
            DistrictHeating district when right is DistrictHeating other =>
                district.NominalCapacityWatts == other.NominalCapacityWatts &&
                district.PumpMotorEfficiency == other.PumpMotorEfficiency &&
                district.SetpointTemperatureCelsius == other.SetpointTemperatureCelsius,
            Chiller chiller when right is Chiller other =>
                chiller.ReferenceCoefficientOfPerformance == other.ReferenceCoefficientOfPerformance &&
                chiller.Compressor == other.Compressor &&
                chiller.NominalCapacityWatts == other.NominalCapacityWatts &&
                chiller.PumpMotorEfficiency == other.PumpMotorEfficiency &&
                chiller.SetpointTemperatureCelsius == other.SetpointTemperatureCelsius &&
                CoolingTowersEqual(chiller.CoolingTower, other.CoolingTower),
            AbsorptionChiller chiller when right is AbsorptionChiller other =>
                chiller.ThermalCoefficientOfPerformance == other.ThermalCoefficientOfPerformance &&
                chiller.NominalCapacityWatts == other.NominalCapacityWatts &&
                chiller.PumpMotorEfficiency == other.PumpMotorEfficiency &&
                chiller.SetpointTemperatureCelsius == other.SetpointTemperatureCelsius &&
                SourceDefinitionsEqual(chiller.HeatSource, other.HeatSource) &&
                CoolingTowersEqual(chiller.CoolingTower, other.CoolingTower),
            _ => false,
        };
    }

    private static bool CoolingTowersEqual(CoolingTower left, CoolingTower right) =>
        left.GetType() == right.GetType() &&
        left.Id.Equals(right.Id) &&
        string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
        left.NominalCapacityWatts == right.NominalCapacityWatts &&
        left.PumpMotorEfficiency == right.PumpMotorEfficiency;
}
