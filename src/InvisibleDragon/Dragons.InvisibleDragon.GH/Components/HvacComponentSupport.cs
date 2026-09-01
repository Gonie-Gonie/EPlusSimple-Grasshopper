using System.Globalization;
using Grasshopper.Kernel.Types;
using Dragons.InvisibleDragon.Grasshopper.Types;
using Dragons.InvisibleDragon.Hvac;

namespace Dragons.InvisibleDragon.Grasshopper.Components;

internal static class HvacComponentSupport
{
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

    internal static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
