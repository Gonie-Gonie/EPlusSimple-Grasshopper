using System.Globalization;
using System.Text.Json.Serialization;
using GonieGonie.BuildingEnergy.Contracts.Internal;

namespace GonieGonie.BuildingEnergy.Contracts;

/// <summary>
/// Serializable state from which deterministic identifier generation can resume.
/// </summary>
public sealed record DeterministicIdGeneratorState
{
    /// <summary>
    /// Creates a generator state.
    /// </summary>
    [JsonConstructor]
    public DeterministicIdGeneratorState(string prefix, long nextSequence, int minimumDigits)
    {
        Prefix = DeterministicIdGenerator.ValidatePrefix(prefix, nameof(prefix));

        if (nextSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextSequence),
                nextSequence,
                "The next sequence must be non-negative.");
        }

        DeterministicIdGenerator.ValidateMinimumDigits(minimumDigits, nameof(minimumDigits));
        NextSequence = nextSequence;
        MinimumDigits = minimumDigits;
    }

    /// <summary>
    /// Gets the identifier prefix.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Gets the sequence that will be issued next.
    /// </summary>
    public long NextSequence { get; }

    /// <summary>
    /// Gets the minimum number of digits in a formatted sequence.
    /// </summary>
    public int MinimumDigits { get; }
}

/// <summary>
/// Issues reproducible, culture-independent identifiers in a caller-defined order.
/// </summary>
public sealed class DeterministicIdGenerator
{
    private const int MaximumMinimumDigits = 32;
    private readonly object _syncRoot = new();
    private long _nextSequence;

    /// <summary>
    /// Creates a generator that starts at sequence one.
    /// </summary>
    public DeterministicIdGenerator(
        string prefix,
        long firstSequence = 1,
        int minimumDigits = 6)
        : this(new DeterministicIdGeneratorState(prefix, firstSequence, minimumDigits))
    {
    }

    /// <summary>
    /// Restores a generator from previously captured state.
    /// </summary>
    public DeterministicIdGenerator(DeterministicIdGeneratorState state)
    {
        ContractGuard.NotNull(state, nameof(state));

        Prefix = state.Prefix;
        MinimumDigits = state.MinimumDigits;
        _nextSequence = state.NextSequence;
    }

    /// <summary>
    /// Gets the stable prefix prepended to each sequence.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Gets the minimum sequence width.
    /// </summary>
    public int MinimumDigits { get; }

    /// <summary>
    /// Gets the sequence that will be issued next.
    /// </summary>
    public long NextSequence
    {
        get
        {
            lock (_syncRoot)
            {
                return _nextSequence;
            }
        }
    }

    /// <summary>
    /// Formats an identifier for a sequence without changing generator state.
    /// </summary>
    public EntityId At(long sequence)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                "The sequence must be non-negative.");
        }

        string numericPart = sequence.ToString(
            "D" + MinimumDigits.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);
        return new EntityId(Prefix + "-" + numericPart);
    }

    /// <summary>
    /// Issues the next identifier.
    /// </summary>
    public EntityId Next()
    {
        lock (_syncRoot)
        {
            if (_nextSequence == long.MaxValue)
            {
                throw new InvalidOperationException("The identifier sequence is exhausted.");
            }

            EntityId result = At(_nextSequence);
            _nextSequence++;
            return result;
        }
    }

    /// <summary>
    /// Captures enough state to resume at exactly the next identifier.
    /// </summary>
    public DeterministicIdGeneratorState CaptureState()
    {
        lock (_syncRoot)
        {
            return new DeterministicIdGeneratorState(Prefix, _nextSequence, MinimumDigits);
        }
    }

    /// <summary>
    /// Creates a generator scoped by a stable parent identifier.
    /// </summary>
    public static DeterministicIdGenerator ForScope(
        string childPrefix,
        EntityId parentId,
        long firstSequence = 1,
        int minimumDigits = 6)
    {
        string validatedChildPrefix = ValidatePrefix(childPrefix, nameof(childPrefix));
        ContractGuard.NotNull(parentId, nameof(parentId));

        return new DeterministicIdGenerator(
            validatedChildPrefix + "-" + parentId.Value,
            firstSequence,
            minimumDigits);
    }

    internal static string ValidatePrefix(string prefix, string parameterName)
    {
        string validated = ContractGuard.RequiredText(prefix, parameterName);
        if (validated[validated.Length - 1] == '-')
        {
            throw new ArgumentException("The identifier prefix must not end with '-'.", parameterName);
        }

        for (int index = 0; index < validated.Length; index++)
        {
            char character = validated[index];
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                throw new ArgumentException(
                    "The identifier prefix must not contain whitespace or control characters.",
                    parameterName);
            }
        }

        return validated;
    }

    internal static void ValidateMinimumDigits(int minimumDigits, string parameterName)
    {
        if (minimumDigits < 1 || minimumDigits > MaximumMinimumDigits)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                minimumDigits,
                "The minimum digit count must be between 1 and 32.");
        }
    }
}
