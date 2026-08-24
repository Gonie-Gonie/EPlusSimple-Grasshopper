using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GonieGonie.BuildingEnergy.Contracts;

namespace GonieGonie.SimpleDragon.Internal;

internal static class DomainSupport
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
        string present = NotNull(value, parameterName);

        string trimmed = present.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return trimmed;
    }

    public static double FinitePositive(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A finite positive value is required.");
        }

        return value;
    }

    public static double FiniteNonNegative(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A finite non-negative value is required.");
        }

        return value;
    }
}

internal static class DeterministicDomainId
{
    public static EntityId Create(string prefix, params object?[] components)
    {
        var canonical = new StringBuilder();
        for (int index = 0; index < components.Length; index++)
        {
            string value = Format(components[index]);
            canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            canonical.Append(':');
            canonical.Append(value);
            canonical.Append(';');
        }

        byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
        byte[] digest;
#if NET7_0_OR_GREATER
        digest = SHA256.HashData(bytes);
#else
        using (SHA256 algorithm = SHA256.Create())
        {
            digest = algorithm.ComputeHash(bytes);
        }
#endif

        var suffix = new StringBuilder(24);
        for (int index = 0; index < 12; index++)
        {
            suffix.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
        }

        return new EntityId(prefix + "-" + suffix);
    }

    private static string Format(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }
}
