namespace GonieGonie.Dragons.ExampleDefinitions;

internal enum ExampleHostAction
{
    Generate,
    Validate
}

internal sealed class ExampleHostInputs
{
    private const string ActionVariable = "DRAGONS_EXAMPLE_ACTION";
    private const string InvisibleGhaVariable = "DRAGONS_INVISIBLE_GHA";
    private const string SimpleGhaVariable = "DRAGONS_SIMPLE_GHA";
    private const string ExamplesRootVariable = "DRAGONS_EXAMPLES_ROOT";
    private const string OutputVariable = "DRAGONS_EXAMPLES_OUTPUT";
    private const string EnergyPlusGateStatusVariable = "DRAGONS_ENERGYPLUS_GATE_STATUS";
    private const string EnergyPlusGateReasonVariable = "DRAGONS_ENERGYPLUS_GATE_REASON";
    private const string EnergyPlusRootVariable = "DRAGONS_ENERGYPLUS_ROOT";
    private const string EnergyPlusWeatherVariable = "DRAGONS_ENERGYPLUS_WEATHER";
    private const string EnergyPlusTimeoutVariable = "DRAGONS_ENERGYPLUS_WORKFLOW_TIMEOUT_SECONDS";

    private ExampleHostInputs(
        ExampleHostAction action,
        string invisibleGhaPath,
        string simpleGhaPath,
        string examplesRoot,
        string outputDirectory,
        string energyPlusGateStatus,
        string energyPlusGateReason,
        string? energyPlusRuntimeRoot,
        string? energyPlusWeatherPath,
        TimeSpan energyPlusWorkflowTimeout)
    {
        Action = action;
        InvisibleGhaPath = invisibleGhaPath;
        SimpleGhaPath = simpleGhaPath;
        ExamplesRoot = examplesRoot;
        OutputDirectory = outputDirectory;
        EnergyPlusGateStatus = energyPlusGateStatus;
        EnergyPlusGateReason = energyPlusGateReason;
        EnergyPlusRuntimeRoot = energyPlusRuntimeRoot;
        EnergyPlusWeatherPath = energyPlusWeatherPath;
        EnergyPlusWorkflowTimeout = energyPlusWorkflowTimeout;
    }

    internal ExampleHostAction Action { get; }

    internal string InvisibleGhaPath { get; }

    internal string SimpleGhaPath { get; }

    internal string ExamplesRoot { get; }

    internal string OutputDirectory { get; }

    internal string EnergyPlusGateStatus { get; }

    internal string EnergyPlusGateReason { get; }

    internal string? EnergyPlusRuntimeRoot { get; }

    internal string? EnergyPlusWeatherPath { get; }

    internal TimeSpan EnergyPlusWorkflowTimeout { get; }

    internal bool CanRunEnergyPlusWorkflow => string.Equals(
        EnergyPlusGateStatus,
        "ready",
        StringComparison.Ordinal);

    internal IReadOnlyList<string> PluginPaths => new[] { InvisibleGhaPath, SimpleGhaPath };

    internal IReadOnlyList<string> PluginDirectories => PluginPaths
        .Select(path => Path.GetDirectoryName(path)!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    internal static ExampleHostInputs FromEnvironment()
    {
        string actionText = Environment.GetEnvironmentVariable(ActionVariable) ?? "Validate";
        if (!Enum.TryParse(actionText, ignoreCase: true, out ExampleHostAction action))
        {
            throw new InvalidOperationException($"{ActionVariable} must be Generate or Validate.");
        }

        string invisibleGha = RequireFile(InvisibleGhaVariable);
        string simpleGha = RequireFile(SimpleGhaVariable);
        RequireFileName(invisibleGha, "GonieGonie.InvisibleDragon.GH.gha", InvisibleGhaVariable);
        RequireFileName(simpleGha, "GonieGonie.SimpleDragon.GH.gha", SimpleGhaVariable);
        string examplesRoot = RequireDirectoryPath(ExamplesRootVariable);
        string outputDirectory = RequireDirectoryPath(OutputVariable);
        string gateStatus = Environment.GetEnvironmentVariable(EnergyPlusGateStatusVariable)
            ?? "unavailable";
        if (gateStatus is not ("ready" or "unavailable" or "disabled"))
        {
            throw new InvalidOperationException(
                $"{EnergyPlusGateStatusVariable} must be ready, unavailable, or disabled.");
        }

        string gateReason = Environment.GetEnvironmentVariable(EnergyPlusGateReasonVariable)
            ?? "The example launcher did not configure EnergyPlus workflow execution.";
        string? runtimeRoot = OptionalFullPath(EnergyPlusRootVariable);
        string? weatherPath = OptionalFullPath(EnergyPlusWeatherVariable);
        if (string.Equals(gateStatus, "ready", StringComparison.Ordinal))
        {
            if (runtimeRoot is null || !Directory.Exists(runtimeRoot))
            {
                throw new DirectoryNotFoundException(
                    $"{EnergyPlusRootVariable} must identify an existing runtime when the gate is ready: {runtimeRoot}");
            }

            if (weatherPath is null || !File.Exists(weatherPath))
            {
                throw new FileNotFoundException(
                    $"{EnergyPlusWeatherVariable} must identify an EPW file when the gate is ready.",
                    weatherPath);
            }
        }

        string timeoutText = Environment.GetEnvironmentVariable(EnergyPlusTimeoutVariable) ?? "60";
        if (!int.TryParse(
                timeoutText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out int timeoutSeconds)
            || timeoutSeconds is < 15 or > 600)
        {
            throw new InvalidOperationException($"{EnergyPlusTimeoutVariable} must be an integer from 15 to 600.");
        }

        Directory.CreateDirectory(outputDirectory);
        return new ExampleHostInputs(
            action,
            invisibleGha,
            simpleGha,
            examplesRoot,
            outputDirectory,
            gateStatus,
            gateReason,
            runtimeRoot,
            weatherPath,
            TimeSpan.FromSeconds(timeoutSeconds));
    }

    private static string RequireFile(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Environment variable {variable} is required.");
        }

        string fullPath = Path.GetFullPath(value);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"{variable} does not identify a file.", fullPath);
        }

        return fullPath;
    }

    private static string RequireDirectoryPath(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Environment variable {variable} is required.");
        }

        return Path.GetFullPath(value);
    }

    private static string? OptionalFullPath(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value.Trim());
    }

    private static void RequireFileName(string path, string expected, string variable)
    {
        if (!string.Equals(Path.GetFileName(path), expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{variable} must identify {expected}.");
        }
    }
}
