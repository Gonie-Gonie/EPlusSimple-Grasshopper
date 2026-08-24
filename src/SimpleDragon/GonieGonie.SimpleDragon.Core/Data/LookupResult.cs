using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

/// <summary>
/// A non-throwing database lookup result with a stable diagnostic on failure.
/// </summary>
public sealed class LookupResult<T>
    where T : class
{
    internal LookupResult(T? value, IReadOnlyList<Diagnostic> diagnostics)
    {
        Value = value;
        Diagnostics = diagnostics;
    }

    public T? Value { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Found => Value is not null;

    public T Require()
    {
        if (Value is not null)
        {
            return Value;
        }

        string message = Diagnostics.Count == 0
            ? "The database value was not found."
            : Diagnostics[0].Message;
        throw new KeyNotFoundException(message);
    }
}

internal static class LookupResults
{
    public static LookupResult<T> Success<T>(T value)
        where T : class
    {
        DomainSupport.NotNull(value, nameof(value));
        return new LookupResult<T>(value, Array.Empty<Diagnostic>());
    }

    public static LookupResult<T> Failure<T>(Diagnostic diagnostic)
        where T : class
    {
        DomainSupport.NotNull(diagnostic, nameof(diagnostic));
        return new LookupResult<T>(null, new[] { diagnostic });
    }
}
