namespace GonieGonie.EnergyPlus.Runtime;

/// <summary>
/// Classifies failures without conflating invalid caller input with library faults.
/// </summary>
public enum EnergyPlusFailureCategory
{
    None,
    UserInput,
    RuntimeNotFound,
    RuntimeIntegrity,
    RuntimeEnvironment,
    ProcessFailure,
    Cancelled,
    Timeout,
    Internal
}

/// <summary>
/// A stable, serializable description of a runtime or simulation failure.
/// </summary>
public sealed record EnergyPlusFailure(
    EnergyPlusFailureCategory Category,
    string Code,
    string Message,
    string? Detail = null)
{
    internal static EnergyPlusFailure Internal(string code, string message, Exception exception)
    {
        return new EnergyPlusFailure(
            EnergyPlusFailureCategory.Internal,
            code,
            message,
            $"{exception.GetType().FullName}: {exception.Message}");
    }
}
