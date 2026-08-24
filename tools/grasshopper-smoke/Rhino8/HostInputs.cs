namespace GonieGonie.Dragons.GrasshopperSmoke.Rhino8;

internal static class Rhino8Environment
{
    private const string RhinoExecutableVariable = "DRAGONS_RHINO8_EXE";

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
}
