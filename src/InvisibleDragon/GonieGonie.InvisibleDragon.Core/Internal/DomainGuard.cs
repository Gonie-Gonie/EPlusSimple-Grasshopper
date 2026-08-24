namespace GonieGonie.InvisibleDragon.Internal;

internal static class DomainGuard
{
    public static T NotNull<T>(T? value, string parameterName)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return value;
    }

    public static string RequiredText(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return trimmed;
    }

    public static double Finite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A finite value is required.");
        }

        return value;
    }

    public static double Positive(double value, string parameterName)
    {
        Finite(value, parameterName);
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A value greater than zero is required.");
        }

        return value;
    }

    public static double NonNegative(double value, string parameterName)
    {
        Finite(value, parameterName);
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A non-negative value is required.");
        }

        return value;
    }

    public static double InRange(double value, double minimum, double maximum, string parameterName)
    {
        Finite(value, parameterName);
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"A value in the inclusive range [{minimum}, {maximum}] is required.");
        }

        return value;
    }

    public static T[] CopyRequired<T>(IEnumerable<T> values, string parameterName)
        where T : class
    {
        T[] copy = NotNull(values, parameterName).ToArray();
        if (copy.Any(value => value is null))
        {
            throw new ArgumentException("The collection cannot contain null values.", parameterName);
        }

        return copy;
    }
}
