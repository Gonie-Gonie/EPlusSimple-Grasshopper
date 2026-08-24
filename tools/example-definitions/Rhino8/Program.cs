using RhinoInside;

namespace GonieGonie.Dragons.ExampleDefinitions.Rhino8;

internal static class Program
{
    static Program()
    {
        Resolver.Initialize(Rhino8Environment.ResolveRhinoSystemDirectory());
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            ExampleHostInputs inputs = ExampleHostInputs.FromEnvironment();
            ConfigureAssemblyResolution(inputs.PluginDirectories);
            return Rhino8Host.Run(inputs);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Dragon example Rhino 8 host failed.");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ConfigureAssemblyResolution(IReadOnlyList<string> pluginDirectories)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) =>
        {
            string fileName = new System.Reflection.AssemblyName(eventArgs.Name).Name + ".dll";
            foreach (string directory in pluginDirectories)
            {
                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    return System.Reflection.Assembly.LoadFrom(candidate);
                }
            }

            return null;
        };
    }

}

internal static class Rhino8Environment
{
    private const string RhinoExecutableVariable = "DRAGONS_RHINO8_EXE";

    internal static string ResolveRhinoSystemDirectory()
    {
        string executable = Environment.GetEnvironmentVariable(RhinoExecutableVariable)
            ?? @"C:\Program Files\Rhino 8\System\Rhino.exe";
        string fullExecutable = Path.GetFullPath(executable);
        string? systemDirectory = Path.GetDirectoryName(fullExecutable);
        if (string.IsNullOrWhiteSpace(systemDirectory)
            || !File.Exists(fullExecutable)
            || !File.Exists(Path.Combine(systemDirectory, "RhinoCommon.dll")))
        {
            throw new InvalidOperationException(
                $"{RhinoExecutableVariable} must identify an installed Rhino 8 executable.");
        }

        return systemDirectory;
    }
}
