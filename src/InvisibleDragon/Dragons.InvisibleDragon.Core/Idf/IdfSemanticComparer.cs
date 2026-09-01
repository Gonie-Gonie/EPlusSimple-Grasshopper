using System.Collections.ObjectModel;
using System.Globalization;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Idf;

/// <summary>
/// Identifies how one IDF value participates in semantic name canonicalization.
/// </summary>
public enum IdfSemanticValueRole
{
    Value,
    ObjectIdentity,
    ObjectReference,
}

/// <summary>
/// Describes one normalized IDF value passed to a semantic canonicalizer.
/// </summary>
public sealed class IdfSemanticValueContext
{
    internal IdfSemanticValueContext(
        string objectType,
        int fieldIndex,
        IddFieldDefinition? fieldDefinition,
        IdfSemanticValueRole role,
        string value)
    {
        ObjectType = objectType;
        FieldIndex = fieldIndex;
        FieldDefinition = fieldDefinition;
        Role = role;
        Value = value;
    }

    public string ObjectType { get; }

    public int FieldIndex { get; }

    public IddFieldDefinition? FieldDefinition { get; }

    public IdfSemanticValueRole Role { get; }

    public string Value { get; }
}

/// <summary>
/// Categorizes a structured semantic difference.
/// </summary>
public enum IdfSemanticMismatchKind
{
    Document,
    ObjectCount,
    MissingObject,
    UnexpectedObject,
    FieldCount,
    FieldValue,
}

/// <summary>
/// One structured semantic difference between expected and actual IDF documents.
/// </summary>
public sealed class IdfSemanticMismatch
{
    internal IdfSemanticMismatch(
        IdfSemanticMismatchKind kind,
        string path,
        string? expected,
        string? actual)
    {
        Kind = kind;
        Path = path;
        Expected = expected;
        Actual = actual;
    }

    public IdfSemanticMismatchKind Kind { get; }

    public string Path { get; }

    public string? Expected { get; }

    public string? Actual { get; }

    public override string ToString()
    {
        return $"{Path}: expected '{Expected ?? "<missing>"}', actual '{Actual ?? "<missing>"}'";
    }
}

/// <summary>
/// The complete structured result of an IDF semantic comparison.
/// </summary>
public sealed class IdfSemanticComparisonResult
{
    internal IdfSemanticComparisonResult(IEnumerable<IdfSemanticMismatch> mismatches)
    {
        Mismatches = new ReadOnlyCollection<IdfSemanticMismatch>(mismatches.ToArray());
    }

    public bool AreEquivalent => Mismatches.Count == 0;

    public IReadOnlyList<IdfSemanticMismatch> Mismatches { get; }
}

/// <summary>
/// Compares IDF meaning while ignoring comments, formatting, object order,
/// numeric spelling, and trailing blank fields.
/// </summary>
public sealed class IdfSemanticComparer : IEqualityComparer<IdfDocument>
{
    private const string RootPath = "$";
    private readonly Func<IdfSemanticValueContext, string>? valueCanonicalizer;

    /// <summary>
    /// Gets the backwards-compatible exact-numeric comparer.
    /// </summary>
    public static IdfSemanticComparer Default { get; } = new();

    /// <summary>
    /// Initializes an order-independent comparer with exact numeric semantics.
    /// </summary>
    public IdfSemanticComparer()
        : this(0, 0)
    {
    }

    /// <summary>
    /// Initializes an order-independent comparer with configurable numeric tolerances
    /// and an optional hook for canonicalizing generated identities and references.
    /// </summary>
    /// <param name="absoluteTolerance">Maximum absolute numeric difference.</param>
    /// <param name="relativeTolerance">
    /// Maximum numeric difference relative to the larger absolute operand.
    /// </param>
    /// <param name="valueCanonicalizer">
    /// Optional pure function that maps normalized values to stable semantic values.
    /// The context role distinguishes object identities, references, and ordinary values.
    /// </param>
    public IdfSemanticComparer(
        double absoluteTolerance,
        double relativeTolerance,
        Func<IdfSemanticValueContext, string>? valueCanonicalizer = null)
    {
        AbsoluteTolerance = ValidateTolerance(absoluteTolerance, nameof(absoluteTolerance));
        RelativeTolerance = ValidateTolerance(relativeTolerance, nameof(relativeTolerance));
        this.valueCanonicalizer = valueCanonicalizer;
    }

    public double AbsoluteTolerance { get; }

    public double RelativeTolerance { get; }

    public bool Equals(IdfDocument? x, IdfDocument? y)
    {
        return Compare(x, y).AreEquivalent;
    }

    /// <summary>
    /// Compares two documents and returns every detected structured mismatch.
    /// </summary>
    public IdfSemanticComparisonResult Compare(IdfDocument? expected, IdfDocument? actual)
    {
        var mismatches = new List<IdfSemanticMismatch>();
        if (ReferenceEquals(expected, actual))
        {
            return new IdfSemanticComparisonResult(mismatches);
        }

        if (expected is null || actual is null)
        {
            mismatches.Add(new IdfSemanticMismatch(
                IdfSemanticMismatchKind.Document,
                RootPath,
                expected is null ? null : "document",
                actual is null ? null : "document"));
            return new IdfSemanticComparisonResult(mismatches);
        }

        Dictionary<string, List<IdfObject>> expectedGroups = GroupByObjectType(expected);
        Dictionary<string, List<IdfObject>> actualGroups = GroupByObjectType(actual);
        string[] objectTypes = expectedGroups.Keys
            .Concat(actualGroups.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string objectType in objectTypes)
        {
            expectedGroups.TryGetValue(objectType, out List<IdfObject>? expectedObjects);
            actualGroups.TryGetValue(objectType, out List<IdfObject>? actualObjects);
            CompareObjectGroup(
                objectType,
                expectedObjects ?? new List<IdfObject>(),
                actualObjects ?? new List<IdfObject>(),
                mismatches);
        }

        return new IdfSemanticComparisonResult(mismatches);
    }

    /// <summary>
    /// Returns an order-independent structural hash. Field values are intentionally
    /// omitted because absolute and relative tolerance equality cannot be partitioned
    /// into stable numeric hash buckets.
    /// </summary>
    public int GetHashCode(IdfDocument obj)
    {
        Guard.NotNull(obj, nameof(obj));

        string[] shapes = obj
            .Select(item => item.ObjectType.ToUpperInvariant() +
                "\u001f" +
                SemanticFieldCount(item).ToString(CultureInfo.InvariantCulture))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        unchecked
        {
            int hash = 17;
            foreach (string shape in shapes)
            {
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(shape);
            }

            return hash;
        }
    }

    public static bool AreEquivalent(IdfDocument? left, IdfDocument? right)
    {
        return Default.Equals(left, right);
    }

    private static Dictionary<string, List<IdfObject>> GroupByObjectType(IdfDocument document)
    {
        var groups = new Dictionary<string, List<IdfObject>>(StringComparer.OrdinalIgnoreCase);
        foreach (IdfObject item in document)
        {
            if (!groups.TryGetValue(item.ObjectType, out List<IdfObject>? values))
            {
                values = new List<IdfObject>();
                groups.Add(item.ObjectType, values);
            }

            values.Add(item);
        }

        return groups;
    }

    private void CompareObjectGroup(
        string objectType,
        IReadOnlyList<IdfObject> expected,
        IReadOnlyList<IdfObject> actual,
        List<IdfSemanticMismatch> mismatches)
    {
        string groupPath = $"{RootPath}.objects[{EscapePathPart(objectType)}]";
        if (expected.Count != actual.Count)
        {
            mismatches.Add(new IdfSemanticMismatch(
                IdfSemanticMismatchKind.ObjectCount,
                $"{groupPath}.count",
                expected.Count.ToString(CultureInfo.InvariantCulture),
                actual.Count.ToString(CultureInfo.InvariantCulture)));
        }

        var remainingExpected = new HashSet<int>(Enumerable.Range(0, expected.Count));
        var remainingActual = new HashSet<int>(Enumerable.Range(0, actual.Count));

        MatchUniqueIdentities(expected, actual, remainingExpected, remainingActual, mismatches);
        MatchEquivalentObjects(expected, actual, remainingExpected, remainingActual);
        MatchClosestObjects(expected, actual, remainingExpected, remainingActual, mismatches);

        foreach (int index in remainingExpected.OrderBy(value => value))
        {
            IdfObject item = expected[index];
            mismatches.Add(new IdfSemanticMismatch(
                IdfSemanticMismatchKind.MissingObject,
                GetObjectPath(item),
                DescribeObject(item),
                null));
        }

        foreach (int index in remainingActual.OrderBy(value => value))
        {
            IdfObject item = actual[index];
            mismatches.Add(new IdfSemanticMismatch(
                IdfSemanticMismatchKind.UnexpectedObject,
                GetObjectPath(item),
                null,
                DescribeObject(item)));
        }
    }

    private void MatchUniqueIdentities(
        IReadOnlyList<IdfObject> expected,
        IReadOnlyList<IdfObject> actual,
        HashSet<int> remainingExpected,
        HashSet<int> remainingActual,
        List<IdfSemanticMismatch> mismatches)
    {
        Dictionary<string, List<int>> expectedByIdentity = IndexByIdentity(expected);
        Dictionary<string, List<int>> actualByIdentity = IndexByIdentity(actual);
        foreach (KeyValuePair<string, List<int>> pair in expectedByIdentity.OrderBy(
                     item => item.Key,
                     StringComparer.Ordinal))
        {
            if (pair.Value.Count != 1 ||
                !actualByIdentity.TryGetValue(pair.Key, out List<int>? actualIndexes) ||
                actualIndexes.Count != 1)
            {
                continue;
            }

            int expectedIndex = pair.Value[0];
            int actualIndex = actualIndexes[0];
            CompareObjects(expected[expectedIndex], actual[actualIndex], mismatches);
            remainingExpected.Remove(expectedIndex);
            remainingActual.Remove(actualIndex);
        }
    }

    private void MatchEquivalentObjects(
        IReadOnlyList<IdfObject> expected,
        IReadOnlyList<IdfObject> actual,
        HashSet<int> remainingExpected,
        HashSet<int> remainingActual)
    {
        int[] expectedIndexes = remainingExpected.OrderBy(value => value).ToArray();
        var actualMatches = new Dictionary<int, int>();
        foreach (int expectedIndex in expectedIndexes)
        {
            var visited = new HashSet<int>();
            TryMatchEquivalent(
                expectedIndex,
                expected,
                actual,
                remainingActual,
                actualMatches,
                visited);
        }

        foreach (KeyValuePair<int, int> match in actualMatches)
        {
            remainingExpected.Remove(match.Value);
            remainingActual.Remove(match.Key);
        }
    }

    private bool TryMatchEquivalent(
        int expectedIndex,
        IReadOnlyList<IdfObject> expected,
        IReadOnlyList<IdfObject> actual,
        HashSet<int> remainingActual,
        Dictionary<int, int> actualMatches,
        HashSet<int> visited)
    {
        foreach (int actualIndex in remainingActual.OrderBy(value => value))
        {
            if (!visited.Add(actualIndex) ||
                !ObjectsEquivalent(expected[expectedIndex], actual[actualIndex]))
            {
                continue;
            }

            if (!actualMatches.TryGetValue(actualIndex, out int previousExpected) ||
                TryMatchEquivalent(
                    previousExpected,
                    expected,
                    actual,
                    remainingActual,
                    actualMatches,
                    visited))
            {
                actualMatches[actualIndex] = expectedIndex;
                return true;
            }
        }

        return false;
    }

    private void MatchClosestObjects(
        IReadOnlyList<IdfObject> expected,
        IReadOnlyList<IdfObject> actual,
        HashSet<int> remainingExpected,
        HashSet<int> remainingActual,
        List<IdfSemanticMismatch> mismatches)
    {
        while (remainingExpected.Count > 0 && remainingActual.Count > 0)
        {
            int selectedExpected = -1;
            int selectedActual = -1;
            int selectedScore = int.MaxValue;
            foreach (int expectedIndex in remainingExpected.OrderBy(value => value))
            {
                foreach (int actualIndex in remainingActual.OrderBy(value => value))
                {
                    int score = DifferenceScore(expected[expectedIndex], actual[actualIndex]);
                    if (score < selectedScore)
                    {
                        selectedExpected = expectedIndex;
                        selectedActual = actualIndex;
                        selectedScore = score;
                    }
                }
            }

            CompareObjects(expected[selectedExpected], actual[selectedActual], mismatches);
            remainingExpected.Remove(selectedExpected);
            remainingActual.Remove(selectedActual);
        }
    }

    private Dictionary<string, List<int>> IndexByIdentity(IReadOnlyList<IdfObject> values)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index++)
        {
            string? identity = GetCanonicalIdentity(values[index]);
            if (identity is null)
            {
                continue;
            }

            if (!result.TryGetValue(identity, out List<int>? indexes))
            {
                indexes = new List<int>();
                result.Add(identity, indexes);
            }

            indexes.Add(index);
        }

        return result;
    }

    private bool ObjectsEquivalent(IdfObject expected, IdfObject actual)
    {
        if (!string.Equals(expected.ObjectType, actual.ObjectType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int expectedCount = SemanticFieldCount(expected);
        int actualCount = SemanticFieldCount(actual);
        if (expectedCount != actualCount)
        {
            return false;
        }

        for (int index = 0; index < expectedCount; index++)
        {
            IddFieldDefinition? expectedDefinition = expected.Definition?.ResolveField(index);
            IddFieldDefinition? actualDefinition = actual.Definition?.ResolveField(index);
            if (!ValuesEqual(
                    expected,
                    actual,
                    index,
                    expectedDefinition,
                    actualDefinition))
            {
                return false;
            }
        }

        return true;
    }

    private void CompareObjects(
        IdfObject expected,
        IdfObject actual,
        List<IdfSemanticMismatch> mismatches)
    {
        string objectPath = GetObjectPath(expected);
        int expectedCount = SemanticFieldCount(expected);
        int actualCount = SemanticFieldCount(actual);
        if (expectedCount != actualCount)
        {
            mismatches.Add(new IdfSemanticMismatch(
                IdfSemanticMismatchKind.FieldCount,
                $"{objectPath}.fields.count",
                expectedCount.ToString(CultureInfo.InvariantCulture),
                actualCount.ToString(CultureInfo.InvariantCulture)));
        }

        int commonCount = Math.Min(expectedCount, actualCount);
        for (int index = 0; index < commonCount; index++)
        {
            IddFieldDefinition? expectedDefinition = expected.Definition?.ResolveField(index);
            IddFieldDefinition? actualDefinition = actual.Definition?.ResolveField(index);
            if (ValuesEqual(
                    expected,
                    actual,
                    index,
                    expectedDefinition,
                    actualDefinition))
            {
                continue;
            }

            string fieldName = expectedDefinition?.Name ?? actualDefinition?.Name ?? $"#{index + 1}";
            mismatches.Add(new IdfSemanticMismatch(
                IdfSemanticMismatchKind.FieldValue,
                $"{objectPath}.fields[{index}:{EscapePathPart(fieldName)}]",
                expected[index],
                actual[index]));
        }
    }

    private int DifferenceScore(IdfObject expected, IdfObject actual)
    {
        int expectedCount = SemanticFieldCount(expected);
        int actualCount = SemanticFieldCount(actual);
        int score = Math.Abs(expectedCount - actualCount);
        int commonCount = Math.Min(expectedCount, actualCount);
        for (int index = 0; index < commonCount; index++)
        {
            if (!ValuesEqual(
                    expected,
                    actual,
                    index,
                    expected.Definition?.ResolveField(index),
                    actual.Definition?.ResolveField(index)))
            {
                score++;
            }
        }

        return score;
    }

    private bool ValuesEqual(
        IdfObject expectedObject,
        IdfObject actualObject,
        int fieldIndex,
        IddFieldDefinition? expectedDefinition,
        IddFieldDefinition? actualDefinition)
    {
        IdfSemanticValueRole role = ResolveRole(
            expectedObject,
            actualObject,
            fieldIndex,
            expectedDefinition,
            actualDefinition);
        string expected = Canonicalize(
            expectedObject,
            fieldIndex,
            expectedDefinition,
            role,
            expectedObject[fieldIndex]);
        string actual = Canonicalize(
            actualObject,
            fieldIndex,
            actualDefinition,
            role,
            actualObject[fieldIndex]);

        if (double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out double expectedNumber) &&
            double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out double actualNumber))
        {
            return NumbersEqual(expectedNumber, actualNumber);
        }

        bool retainsCase = expectedDefinition?.RetainsCase == true || actualDefinition?.RetainsCase == true;
        return string.Equals(
            expected,
            actual,
            retainsCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    private bool NumbersEqual(double expected, double actual)
    {
        if (expected.Equals(actual))
        {
            return true;
        }

        if (double.IsNaN(expected) || double.IsNaN(actual) ||
            double.IsInfinity(expected) || double.IsInfinity(actual))
        {
            return false;
        }

        double difference = Math.Abs(expected - actual);
        double relativeScale = Math.Max(Math.Abs(expected), Math.Abs(actual));
        double permittedDifference = AbsoluteTolerance + (RelativeTolerance * relativeScale);
        return difference <= permittedDifference;
    }

    private string Canonicalize(
        IdfObject item,
        int fieldIndex,
        IddFieldDefinition? definition,
        IdfSemanticValueRole role,
        string value)
    {
        string normalized = Normalize(value);
        if (valueCanonicalizer is null)
        {
            return normalized;
        }

        string canonical = valueCanonicalizer(new IdfSemanticValueContext(
            item.ObjectType,
            fieldIndex,
            definition,
            role,
            normalized));
        if (canonical is null)
        {
            throw new InvalidOperationException("The IDF semantic value canonicalizer returned null.");
        }

        return Normalize(canonical);
    }

    private string? GetCanonicalIdentity(IdfObject item)
    {
        int? identityIndex = ResolveIdentityIndex(item);
        if (identityIndex is null || identityIndex.Value >= SemanticFieldCount(item))
        {
            return null;
        }

        IddFieldDefinition? definition = item.Definition?.ResolveField(identityIndex.Value);
        string value = Canonicalize(
            item,
            identityIndex.Value,
            definition,
            IdfSemanticValueRole.ObjectIdentity,
            item[identityIndex.Value]);
        if (value.Length == 0)
        {
            return null;
        }

        string canonicalCase = definition?.RetainsCase == true
            ? $"C:{value}"
            : $"I:{value.ToUpperInvariant()}";
        return $"{item.ObjectType.ToUpperInvariant()}\u001f{canonicalCase}";
    }

    private static int? ResolveIdentityIndex(IdfObject item)
    {
        if (item.Definition is not null)
        {
            return item.Definition.TryGetField("Name", out IddFieldDefinition? nameField)
                ? nameField!.Position
                : null;
        }

        return item.Count == 0 ? null : 0;
    }

    private static IdfSemanticValueRole ResolveRole(
        IdfObject expectedObject,
        IdfObject actualObject,
        int fieldIndex,
        IddFieldDefinition? expectedDefinition,
        IddFieldDefinition? actualDefinition)
    {
        if (ResolveIdentityIndex(expectedObject) == fieldIndex ||
            ResolveIdentityIndex(actualObject) == fieldIndex)
        {
            return IdfSemanticValueRole.ObjectIdentity;
        }

        if (IsReference(expectedDefinition) || IsReference(actualDefinition))
        {
            return IdfSemanticValueRole.ObjectReference;
        }

        return IdfSemanticValueRole.Value;
    }

    private static bool IsReference(IddFieldDefinition? definition)
    {
        return definition is not null &&
            (definition.DataType == IddDataType.ObjectList ||
             definition.DataType == IddDataType.Node ||
             definition.ObjectLists.Count > 0);
    }

    private string GetObjectPath(IdfObject item)
    {
        string type = EscapePathPart(item.ObjectType);
        string? identity = GetCanonicalIdentity(item);
        if (identity is null)
        {
            return $"{RootPath}.objects[{type}]";
        }

        int separator = identity.IndexOf('\u001f');
        string display = separator < 0 ? identity : identity.Substring(separator + 1);
        if (display.StartsWith("C:", StringComparison.Ordinal) ||
            display.StartsWith("I:", StringComparison.Ordinal))
        {
            display = display.Substring(2);
        }

        return $"{RootPath}.objects[{type}:{EscapePathPart(display)}]";
    }

    private static string DescribeObject(IdfObject item)
    {
        string? name = item.Name;
        return string.IsNullOrWhiteSpace(name) ? item.ObjectType : $"{item.ObjectType}:{name}";
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

    private static string Normalize(string value)
    {
        string normalized = value.Trim();
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[normalized.Length - 1] == '"')
        {
            normalized = normalized.Substring(1, normalized.Length - 2).Replace("\"\"", "\"");
        }

        return normalized;
    }

    private static string EscapePathPart(string value)
    {
        return value.Replace("\\", "\\\\").Replace("]", "\\]").Replace(":", "\\:");
    }

    private static double ValidateTolerance(double value, string parameterName)
    {
        if (value < 0 || double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, "A finite, non-negative tolerance is required.");
        }

        return value;
    }
}
