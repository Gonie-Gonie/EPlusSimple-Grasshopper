using System.Reflection;
using RhinoInside;

namespace Dragons.ExampleDefinitions.Rhino7;

internal static class Program
{
    private static readonly List<string> ProbeDirectories = new();
    [STAThread]
    private static int Main()
    {
        try
        {
            ExampleHostInputs inputs = ExampleHostInputs.FromEnvironment();
            string rhinoExecutable = Environment.GetEnvironmentVariable("DRAGONS_RHINO7_EXE")
                ?? @"C:\Program Files\Rhino 7\System\Rhino.exe";
            string rhinoSystem = Path.GetDirectoryName(Path.GetFullPath(rhinoExecutable))
                ?? throw new InvalidOperationException("The Rhino 7 executable has no parent directory.");
            string grasshopperDirectory = Path.GetFullPath(
                Path.Combine(rhinoSystem, "..", "Plug-ins", "Grasshopper"));
            RequireFile(rhinoExecutable, "Rhino 7 executable");
            RequireFile(Path.Combine(rhinoSystem, "RhinoCommon.dll"), "Rhino 7 RhinoCommon");
            RequireFile(Path.Combine(grasshopperDirectory, "Grasshopper.dll"), "Rhino 7 Grasshopper");
            ProbeDirectories.Add(rhinoSystem);
            ProbeDirectories.Add(grasshopperDirectory);
            ProbeDirectories.AddRange(inputs.PluginDirectories);
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            Environment.SetEnvironmentVariable(
                "PATH",
                string.Join(
                    ";",
                    new[] { rhinoSystem, grasshopperDirectory, Environment.GetEnvironmentVariable("PATH") }));

            Resolver.Initialize();
            return Rhino7Host.Run(inputs);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Dragon example Rhino 7 host failed.");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static Assembly? ResolveAssembly(object? sender, ResolveEventArgs eventArgs)
    {
        string fileName = new AssemblyName(eventArgs.Name).Name + ".dll";
        foreach (string directory in ProbeDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return Assembly.LoadFrom(candidate);
            }
        }

        return null;
    }

    private static void RequireFile(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(label + " was not found.", path);
        }
    }
}
