using System.Reflection;
using Rhino;

namespace GonieGonie.Dragons.GrasshopperSmoke.Rhino8;

internal static class RhinoHost
{
    private const string GrasshopperPlugInName = "Grasshopper";

    public static int Run(SmokeHostInputs inputs, string[] coreArguments)
    {
        Progress("isolating installed Dragon package folders");
        GrasshopperSmokeGate.BlockInstalledDragonPackages();
        Progress("starting RhinoCore");
        using var core = new Rhino.Runtime.InProcess.RhinoCore(coreArguments);
        Progress("RhinoCore started");
        Check(RhinoApp.Version.Major == 8, "Rhino 8 runtime was not loaded.");

        Progress("loading installed Grasshopper plug-in object");
        object grasshopper = RhinoApp.GetPlugInObject(GrasshopperPlugInName)
            ?? throw new InvalidOperationException("The installed Grasshopper plug-in could not be loaded by Rhino 8.");

        Progress("restricting Grasshopper external libraries to the requested Dragon GHA set");
        GrasshopperSmokeGate.RestrictExternalLibraries(inputs.PluginPaths);
        Progress("running Grasshopper headless initialization");
        InvokeRunHeadless(grasshopper);
        Progress("Grasshopper headless initialization completed");

        GrasshopperSmokeSummary summary = GrasshopperSmokeGate.Run(
            inputs,
            "Rhino8",
            RhinoApp.Version.ToString());
        Console.WriteLine(summary.ToConsoleText());
        return 0;
    }

    private static void Progress(string message)
    {
        Console.WriteLine($"[grasshopper-smoke] {message}");
        Console.Out.Flush();
    }

    private static void InvokeRunHeadless(object grasshopper)
    {
        MethodInfo method = grasshopper.GetType().GetMethod(
            "RunHeadless",
            BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(grasshopper.GetType().FullName, "RunHeadless");
        method.Invoke(grasshopper, null);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
