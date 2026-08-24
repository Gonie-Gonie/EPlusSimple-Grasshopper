namespace GonieGonie.BuildingEnergy.Contracts;

/// <summary>
/// Describes the lifecycle state captured by a run manifest.
/// </summary>
public enum RunStatus
{
    /// <summary>
    /// The run has started and has no terminal outcome yet.
    /// </summary>
    Running = 0,

    /// <summary>
    /// The run completed successfully.
    /// </summary>
    Succeeded = 1,

    /// <summary>
    /// The run completed with a failure.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// The run was cancelled.
    /// </summary>
    Cancelled = 3,
}
