using System.Reflection;
using RhinoInside;

namespace GonieGonie.Dragons.Grasshopper.Rhino7Probe;

internal static class Program
{
    private static readonly List<string> ProbeDirectories = new();

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("usage: Rhino7Probe <InvisibleDragon.gha> <SimpleDragon.gha> <output.gh>");
            return 2;
        }

        string rhinoExecutable = Environment.GetEnvironmentVariable("DRAGONS_RHINO7_EXE")
            ?? @"C:\Program Files\Rhino 7\System\Rhino.exe";
        string rhinoSystem = Path.GetDirectoryName(Path.GetFullPath(rhinoExecutable))
            ?? throw new InvalidOperationException("The Rhino 7 executable has no parent directory.");
        string grasshopper = Path.GetFullPath(Path.Combine(rhinoSystem, "..", "Plug-ins", "Grasshopper"));
        RequireFile(rhinoExecutable, "Rhino 7 executable");
        RequireFile(Path.Combine(rhinoSystem, "RhinoCommon.dll"), "Rhino 7 RhinoCommon");
        RequireFile(Path.Combine(grasshopper, "Grasshopper.dll"), "Rhino 7 Grasshopper");
        ProbeDirectories.Add(rhinoSystem);
        ProbeDirectories.Add(grasshopper);
        ProbeDirectories.Add(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
        ProbeDirectories.Add(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);

        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        Environment.SetEnvironmentVariable(
            "PATH",
            string.Join(";", new[] { rhinoSystem, grasshopper, Environment.GetEnvironmentVariable("PATH") }));

        try
        {
            Resolver.Initialize();
            return Rhino7InsideHost.Run(
                Path.GetFullPath(args[0]),
                Path.GetFullPath(args[1]),
                Path.GetFullPath(args[2]));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void RequireFile(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{label} was not found.", path);
        }
    }

    private static Assembly? ResolveAssembly(object? sender, ResolveEventArgs args)
    {
        string simpleName = new AssemblyName(args.Name).Name + ".dll";
        foreach (string directory in ProbeDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string candidate = Path.Combine(directory, simpleName);
            if (File.Exists(candidate))
            {
                return Assembly.LoadFrom(candidate);
            }
        }

        return null;
    }
}
