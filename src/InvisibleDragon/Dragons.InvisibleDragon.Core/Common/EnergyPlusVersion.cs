using System.Collections;
using System.Globalization;

namespace Dragons.InvisibleDragon;

/// <summary>
/// An immutable EnergyPlus version with the identity equality used by the
/// pinned InvisibleDragon implementation.
/// </summary>
public sealed class EnergyPlusVersion : IReadOnlyList<int>
{
    public EnergyPlusVersion(string version)
    {
        version = Required(version, nameof(version));

        int[] parts = ParseComponents(version);
        if (parts.Length == 2)
        {
            Major = parts[0];
            Minor = parts[1];
            Patch = 0;
            return;
        }

        if (parts.Length == 3)
        {
            Major = parts[0];
            Minor = parts[1];
            Patch = parts[2];
            return;
        }

        throw new ArgumentException(
            $"Expected two or three integer tokens, but found {parts.Length} in the EnergyPlus version.",
            nameof(version));
    }

    public EnergyPlusVersion(int major, int minor)
        : this(major, minor, 0)
    {
    }

    public EnergyPlusVersion(int major, int minor, int patch)
    {
        Major = Nonnegative(major, nameof(major));
        Minor = Nonnegative(minor, nameof(minor));
        Patch = Nonnegative(patch, nameof(patch));
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public int Count => 3;

    public int this[int index] => index switch
    {
        0 => Major,
        1 => Minor,
        2 => Patch,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "A version index must be between zero and two."),
    };

    public string LegacyIddFileName => $"V{Format()}-Energy+.idd";

    public string EnergyPlusDirectoryName => $"EnergyPlusV{Format()}";

    public string Format(string separator = "-")
    {
        separator = Required(separator, nameof(separator));
        return string.Join(separator, this);
    }

    public static EnergyPlusVersion From(EnergyPlusVersion version)
    {
        return Required(version, nameof(version));
    }

    public static EnergyPlusVersion From(string version)
    {
        return new EnergyPlusVersion(version);
    }

    public static EnergyPlusVersion From(IReadOnlyList<int> version)
    {
        version = Required(version, nameof(version));
        if (version is EnergyPlusVersion existing)
        {
            return existing;
        }

        return version.Count switch
        {
            2 => new EnergyPlusVersion(version[0], version[1]),
            3 => new EnergyPlusVersion(version[0], version[1], version[2]),
            _ => throw new ArgumentException(
                "An EnergyPlus version requires two or three integers.",
                nameof(version)),
        };
    }

    public IEnumerator<int> GetEnumerator()
    {
        yield return Major;
        yield return Minor;
        yield return Patch;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private static int Nonnegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "An EnergyPlus version number cannot be negative.");
        }

        return value;
    }

    private static int[] ParseComponents(string version)
    {
        var parts = new List<int>();
        int? current = null;
        for (int index = 0; index < version.Length;)
        {
            int digit = CharUnicodeInfo.GetDecimalDigitValue(version, index);
            int width = char.IsSurrogatePair(version, index) ? 2 : 1;
            if (digit >= 0)
            {
                try
                {
                    current = checked(((current ?? 0) * 10) + digit);
                }
                catch (OverflowException)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(version),
                        version,
                        "Each version token must fit in a nonnegative Int32.");
                }
            }
            else if (current.HasValue)
            {
                parts.Add(current.Value);
                current = null;
            }

            index += width;
        }

        if (current.HasValue)
        {
            parts.Add(current.Value);
        }

        return parts.ToArray();
    }

    private static T Required<T>(T? value, string parameterName)
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
}

public static class EnergyPlusDefaults
{
    public const int DefaultYear = 2026;

    public static EnergyPlusVersion DefaultVersion { get; } = new(24, 2, 0);
}
