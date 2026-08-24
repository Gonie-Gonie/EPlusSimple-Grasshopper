using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using GonieGonie.BuildingEnergy.Contracts.Internal;

namespace GonieGonie.BuildingEnergy.Contracts;

/// <summary>
/// An immutable, ordered collection of validation diagnostics.
/// </summary>
public sealed record ValidationResult
{
    private static readonly IReadOnlyList<Diagnostic> NoDiagnostics = Array.AsReadOnly(Array.Empty<Diagnostic>());

    /// <summary>
    /// Creates a validation result and defensively copies its ordered diagnostics.
    /// </summary>
    [JsonConstructor]
    public ValidationResult(IReadOnlyList<Diagnostic> diagnostics)
    {
        ContractGuard.NotNull(diagnostics, nameof(diagnostics));

        Diagnostic[] copy = new Diagnostic[diagnostics.Count];
        for (int index = 0; index < diagnostics.Count; index++)
        {
            Diagnostic? diagnostic = diagnostics[index];
            if (diagnostic is null)
            {
                throw new ArgumentException("A validation result cannot contain a null diagnostic.", nameof(diagnostics));
            }

            copy[index] = diagnostic;
        }

        Diagnostics = copy.Length == 0
            ? NoDiagnostics
            : new ReadOnlyCollection<Diagnostic>(copy);
    }

    /// <summary>
    /// Gets a successful result with no diagnostics.
    /// </summary>
    public static ValidationResult Success { get; } = new(NoDiagnostics);

    /// <summary>
    /// Gets the diagnostics in deterministic discovery order.
    /// </summary>
    [JsonPropertyOrder(0)]
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets whether no error or fatal diagnostic is present.
    /// </summary>
    [JsonIgnore]
    public bool IsValid => !Diagnostics.Any(diagnostic => diagnostic.IsFailure);

    /// <summary>
    /// Gets whether at least one warning is present.
    /// </summary>
    [JsonIgnore]
    public bool HasWarnings => Diagnostics.Any(
        diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);

    /// <summary>
    /// Gets the highest observed severity, or <see langword="null"/> when empty.
    /// </summary>
    [JsonIgnore]
    public DiagnosticSeverity? HighestSeverity => Diagnostics.Count == 0
        ? null
        : Diagnostics.Max(diagnostic => diagnostic.Severity);

    /// <summary>
    /// Creates a result from any diagnostic sequence, preserving its order.
    /// </summary>
    public static ValidationResult From(IEnumerable<Diagnostic> diagnostics)
    {
        ContractGuard.NotNull(diagnostics, nameof(diagnostics));

        return new ValidationResult(diagnostics.ToArray());
    }

    /// <summary>
    /// Combines results without short-circuiting and preserves result and diagnostic order.
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        ContractGuard.NotNull(results, nameof(results));

        List<Diagnostic> diagnostics = new();
        for (int index = 0; index < results.Length; index++)
        {
            ValidationResult? result = results[index];
            if (result is null)
            {
                throw new ArgumentException("A validation result collection cannot contain null.", nameof(results));
            }

            diagnostics.AddRange(result.Diagnostics);
        }

        return diagnostics.Count == 0 ? Success : new ValidationResult(diagnostics);
    }

    /// <summary>
    /// Returns a new result with one diagnostic appended.
    /// </summary>
    public ValidationResult Add(Diagnostic diagnostic)
    {
        ContractGuard.NotNull(diagnostic, nameof(diagnostic));

        Diagnostic[] diagnostics = new Diagnostic[Diagnostics.Count + 1];
        for (int index = 0; index < Diagnostics.Count; index++)
        {
            diagnostics[index] = Diagnostics[index];
        }

        diagnostics[diagnostics.Length - 1] = diagnostic;
        return new ValidationResult(diagnostics);
    }
}
