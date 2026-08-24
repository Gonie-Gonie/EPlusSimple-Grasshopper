namespace GonieGonie.Dragons.GrasshopperSmoke.Rhino8;

internal sealed record HostInputs(
    string InvisibleDragonGha,
    string SimpleDragonGha,
    string OutputDirectory)
{
    private const string RhinoExecutableVariable = "DRAGONS_RHINO8_EXE";
    private const string InvisibleGhaVariable = "DRAGONS_INVISIBLE_GHA";
    private const string SimpleGhaVariable = "DRAGONS_SIMPLE_GHA";
    private const string OutputVariable = "DRAGONS_GRASSHOPPER_SMOKE_OUTPUT";

    public IReadOnlyList<string> PluginPaths => new[] { InvisibleDragonGha, SimpleDragonGha };

    public string DocumentPath => Path.Combine(OutputDirectory, "dragons-host-gate.gh");

    public string SummaryPath => Path.Combine(OutputDirectory, "summary.json");

    public static HostInputs FromEnvironment()
    {
        string invisible = RequiredFile(InvisibleGhaVariable);
        string simple = RequiredFile(SimpleGhaVariable);
        string output = RequiredDirectoryValue(OutputVariable);
        return new HostInputs(invisible, simple, output);
    }

    public static string ResolveRhinoSystemDirectory()
    {
        string executable = Environment.GetEnvironmentVariable(RhinoExecutableVariable)
            ?? @"C:\Program Files\Rhino 8\System\Rhino.exe";
        string fullExecutable = Path.GetFullPath(executable);
        string? systemDirectory = Path.GetDirectoryName(fullExecutable);
        if (string.IsNullOrWhiteSpace(systemDirectory) ||
            !File.Exists(fullExecutable) ||
            !File.Exists(Path.Combine(systemDirectory, "RhinoCommon.dll")))
        {
            throw new InvalidOperationException(
                $"{RhinoExecutableVariable} must identify an installed Rhino 8 executable.");
        }

        return systemDirectory;
    }

    private static string RequiredFile(string variable)
    {
        string value = Environment.GetEnvironmentVariable(variable)
            ?? throw new InvalidOperationException($"Environment variable {variable} is required.");
        string fullPath = Path.GetFullPath(value);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"The path supplied through {variable} does not exist.", fullPath);
        }

        return fullPath;
    }

    private static string RequiredDirectoryValue(string variable)
    {
        string value = Environment.GetEnvironmentVariable(variable)
            ?? throw new InvalidOperationException($"Environment variable {variable} is required.");
        return Path.GetFullPath(value);
    }
}
