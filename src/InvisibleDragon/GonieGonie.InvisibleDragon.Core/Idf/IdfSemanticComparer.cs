using System.Globalization;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Idf;

/// <summary>
/// Compares IDF meaning while ignoring comments, formatting, numeric spelling, and trailing blank fields.
/// </summary>
public sealed class IdfSemanticComparer : IEqualityComparer<IdfDocument>
{
    public static IdfSemanticComparer Default { get; } = new();

    public bool Equals(IdfDocument? x, IdfDocument? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null || x.Count != y.Count)
        {
            return false;
        }

        for (int index = 0; index < x.Count; index++)
        {
            if (!ObjectsEqual(x[index], y[index]))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(IdfDocument obj)
    {
        Guard.NotNull(obj, nameof(obj));

        unchecked
        {
            int hash = 17;
            foreach (IdfObject item in obj)
            {
                hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(item.ObjectType);
                int count = SemanticFieldCount(item);
                for (int index = 0; index < count; index++)
                {
                    IddFieldDefinition? definition = item.Definition?.ResolveField(index);
                    hash = (hash * 31) + ValueHash(item[index], definition);
                }
            }

            return hash;
        }
    }

    public static bool AreEquivalent(IdfDocument? left, IdfDocument? right)
    {
        return Default.Equals(left, right);
    }

    private static bool ObjectsEqual(IdfObject left, IdfObject right)
    {
        if (!string.Equals(left.ObjectType, right.ObjectType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int leftCount = SemanticFieldCount(left);
        int rightCount = SemanticFieldCount(right);
        if (leftCount != rightCount)
        {
            return false;
        }

        for (int index = 0; index < leftCount; index++)
        {
            IddFieldDefinition? definition = left.Definition?.ResolveField(index) ?? right.Definition?.ResolveField(index);
            if (!ValuesEqual(left[index], right[index], definition))
            {
                return false;
            }
        }

        return true;
    }

    private static int SemanticFieldCount(IdfObject value)
    {
        int count = value.Count;
        while (count > 0 && string.IsNullOrWhiteSpace(value[count - 1]))
        {
            count--;
        }

        return count;
    }

    private static bool ValuesEqual(string left, string right, IddFieldDefinition? definition)
    {
        string normalizedLeft = Normalize(left);
        string normalizedRight = Normalize(right);
        if (double.TryParse(normalizedLeft, NumberStyles.Float, CultureInfo.InvariantCulture, out double leftNumber) &&
            double.TryParse(normalizedRight, NumberStyles.Float, CultureInfo.InvariantCulture, out double rightNumber))
        {
            return leftNumber.Equals(rightNumber);
        }

        return string.Equals(
            normalizedLeft,
            normalizedRight,
            definition?.RetainsCase == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    private static int ValueHash(string value, IddFieldDefinition? definition)
    {
        string normalized = Normalize(value);
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            return number.GetHashCode();
        }

        return (definition?.RetainsCase == true ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
            .GetHashCode(normalized);
    }

    private static string Normalize(string value)
    {
        string normalized = value.Trim();
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[normalized.Length - 1] == '"')
        {
            normalized = normalized.Substring(1, normalized.Length - 2).Replace("\"\"", "\"");
        }

        return normalized;
    }
}
