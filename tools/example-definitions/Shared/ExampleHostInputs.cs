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

    private ExampleHostInputs(
        ExampleHostAction action,
        string invisibleGhaPath,
        string simpleGhaPath,
        string examplesRoot,
        string outputDirectory)
    {
        Action = action;
        InvisibleGhaPath = invisibleGhaPath;
        SimpleGhaPath = simpleGhaPath;
        ExamplesRoot = examplesRoot;
        OutputDirectory = outputDirectory;
    }

    internal ExampleHostAction Action { get; }

    internal string InvisibleGhaPath { get; }

    internal string SimpleGhaPath { get; }

    internal string ExamplesRoot { get; }

    internal string OutputDirectory { get; }

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
        Directory.CreateDirectory(outputDirectory);
        return new ExampleHostInputs(
            action,
            invisibleGha,
            simpleGha,
            examplesRoot,
            outputDirectory);
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

    private static void RequireFileName(string path, string expected, string variable)
    {
        if (!string.Equals(Path.GetFileName(path), expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{variable} must identify {expected}.");
        }
    }
}
