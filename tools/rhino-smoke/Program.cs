using RhinoInside;

namespace Dragons.InvisibleDragon.RhinoSmoke;

internal static class Program
{
    private static readonly string[] CoreArguments = { "/netcore" };

    static Program()
    {
        Resolver.Initialize(ResolveRhinoSystemDirectory());
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            return RhinoSmokeChecks.Run(CoreArguments);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("InvisibleDragon Rhino smoke checks failed.");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static string ResolveRhinoSystemDirectory()
    {
        string executable = Environment.GetEnvironmentVariable("DRAGONS_RHINO8_EXE")
            ?? @"C:\Program Files\Rhino 8\System\Rhino.exe";
        string? systemDirectory = Path.GetDirectoryName(executable);
        if (string.IsNullOrWhiteSpace(systemDirectory) ||
            !File.Exists(Path.Combine(systemDirectory, "Rhino.exe")) ||
            !File.Exists(Path.Combine(systemDirectory, "RhinoCommon.dll")))
        {
            throw new InvalidOperationException(
                "A verified Rhino 8 System directory is required. Run 'dev.cmd setup' after installing Rhino 8.");
        }

        return systemDirectory;
    }
}
