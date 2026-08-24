namespace GonieGonie.BuildingEnergy.Contracts;

/// <summary>
/// Describes the impact of a diagnostic.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Informational context that does not require intervention.
    /// </summary>
    Info = 0,

    /// <summary>
    /// A recoverable condition that the user should review.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Invalid input or a failed operation for part of a model.
    /// </summary>
    Error = 2,

    /// <summary>
    /// A condition that prevents the requested operation from continuing.
    /// </summary>
    Fatal = 3,
}
