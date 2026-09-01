using System.Reflection;
using Dragons.GrasshopperSmoke;
using RhinoInside;

namespace Dragons.Grasshopper.Rhino7Probe;

internal static class Program
{
    private static readonly List<string> ProbeDirectories = new();

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 0 && args.Length != 3)
        {
            Console.Error.WriteLine(
                "usage: Rhino7Probe [<InvisibleDragon.gha> <SimpleDragon.gha> <output.gh>]");
            return 2;
        }

        try
        {
            ApplyLegacyArguments(args);
            SmokeHostInputs inputs = SmokeHostInputs.FromEnvironment();
            Directory.CreateDirectory(inputs.OutputDirectory);

            string rhinoExecutable = Environment.GetEnvironmentVariable("DRAGONS_RHINO7_EXE")
                ?? @"C:\Program Files\Rhino 7\System\Rhino.exe";
            string rhinoSystem = Path.GetDirectoryName(Path.GetFullPath(rhinoExecutable))
                ?? throw new InvalidOperationException("The Rhino 7 executable has no parent directory.");
            string grasshopper = Path.GetFullPath(
                Path.Combine(rhinoSystem, "..", "Plug-ins", "Grasshopper"));
            RequireFile(rhinoExecutable, "Rhino 7 executable");
            RequireFile(Path.Combine(rhinoSystem, "RhinoCommon.dll"), "Rhino 7 RhinoCommon");
            RequireFile(Path.Combine(grasshopper, "Grasshopper.dll"), "Rhino 7 Grasshopper");
            ProbeDirectories.Add(rhinoSystem);
            ProbeDirectories.Add(grasshopper);
            ProbeDirectories.AddRange(inputs.AllowedPluginRoots);

            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            Environment.SetEnvironmentVariable(
                "PATH",
                string.Join(
                    ";",
                    new[] { rhinoSystem, grasshopper, Environment.GetEnvironmentVariable("PATH") }));

            Resolver.Initialize();
            return Rhino7InsideHost.Run(inputs);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ApplyLegacyArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return;
        }

        string invisible = Path.GetFullPath(args[0]);
        string simple = Path.GetFullPath(args[1]);
        string document = Path.GetFullPath(args[2]);
        string output = Path.GetDirectoryName(document)
            ?? throw new InvalidOperationException("The legacy output path has no parent directory.");
        string[] roots = new[] { invisible, simple }
            .Select(path => Path.GetDirectoryName(path)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Environment.SetEnvironmentVariable("DRAGONS_SMOKE_SCENARIO", "Both");
        Environment.SetEnvironmentVariable("DRAGONS_SMOKE_SOURCE", "build-output");
        Environment.SetEnvironmentVariable(
            "DRAGONS_PLUGIN_PATHS",
            string.Join(Path.PathSeparator.ToString(), new[] { invisible, simple }));
        Environment.SetEnvironmentVariable(
            "DRAGONS_ALLOWED_PLUGIN_ROOTS",
            string.Join(Path.PathSeparator.ToString(), roots));
        Environment.SetEnvironmentVariable("DRAGONS_GRASSHOPPER_SMOKE_OUTPUT", output);
        Environment.SetEnvironmentVariable("DRAGONS_GRASSHOPPER_SMOKE_DOCUMENT", document);
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
