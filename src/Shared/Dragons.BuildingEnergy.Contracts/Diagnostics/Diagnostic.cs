using System.Text.Json.Serialization;
using Dragons.BuildingEnergy.Contracts.Internal;

namespace Dragons.BuildingEnergy.Contracts;

/// <summary>
/// A stable, user-facing description of an observed condition.
/// </summary>
public sealed record Diagnostic
{
    /// <summary>
    /// Creates a diagnostic.
    /// </summary>
    [JsonConstructor]
    public Diagnostic(
        string code,
        DiagnosticSeverity severity,
        string message,
        EntityId? objectId = null,
        GeometryProvenance? geometry = null,
        string? suggestedAction = null)
    {
        if (!Enum.IsDefined(typeof(DiagnosticSeverity), severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown diagnostic severity.");
        }

        Code = ValidateCode(code);
        Severity = severity;
        Message = ContractGuard.RequiredText(message, nameof(message));
        ObjectId = objectId;
        Geometry = geometry;
        SuggestedAction = ContractGuard.OptionalText(suggestedAction, nameof(suggestedAction));
    }

    /// <summary>
    /// Gets the stable machine-readable diagnostic code.
    /// </summary>
    [JsonPropertyOrder(0)]
    public string Code { get; }

    /// <summary>
    /// Gets the diagnostic severity.
    /// </summary>
    [JsonPropertyOrder(1)]
    public DiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the human-readable message.
    /// </summary>
    [JsonPropertyOrder(2)]
    public string Message { get; }

    /// <summary>
    /// Gets the related model object identifier, when one is known.
    /// </summary>
    [JsonPropertyOrder(3)]
    public EntityId? ObjectId { get; }

    /// <summary>
    /// Gets the related source geometry, when one is known.
    /// </summary>
    [JsonPropertyOrder(4)]
    public GeometryProvenance? Geometry { get; }

    /// <summary>
    /// Gets an optional user-facing remediation.
    /// </summary>
    [JsonPropertyOrder(5)]
    public string? SuggestedAction { get; }

    /// <summary>
    /// Gets whether the diagnostic invalidates a validation result.
    /// </summary>
    [JsonIgnore]
    public bool IsFailure => Severity >= DiagnosticSeverity.Error;

    private static string ValidateCode(string code)
    {
        string validated = ContractGuard.RequiredText(code, nameof(code));
        for (int index = 0; index < validated.Length; index++)
        {
            char character = validated[index];
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                throw new ArgumentException(
                    "A diagnostic code must not contain whitespace or control characters.",
                    nameof(code));
            }
        }

        return validated;
    }
}
