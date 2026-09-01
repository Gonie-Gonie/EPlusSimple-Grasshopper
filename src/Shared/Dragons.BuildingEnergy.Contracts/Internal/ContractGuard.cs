namespace Dragons.BuildingEnergy.Contracts.Internal;

internal static class ContractGuard
{
    public static T NotNull<T>(T? value, string parameterName)
        where T : class
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
#endif

        return value;
    }

    public static string RequiredText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value must not be null, empty, or whitespace.", parameterName);
        }

        string validated = value!;
        if (!string.Equals(validated, validated.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("The value must not contain leading or trailing whitespace.", parameterName);
        }

        return validated;
    }

    public static string? OptionalText(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        return RequiredText(value, parameterName);
    }
}
