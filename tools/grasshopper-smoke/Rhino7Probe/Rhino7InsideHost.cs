using GonieGonie.Dragons.GrasshopperSmoke;

namespace GonieGonie.Dragons.Grasshopper.Rhino7Probe;

internal static class Rhino7InsideHost
{
    private static readonly string[] CoreArguments = { "/nosplash", "/notemplate" };

    public static int Run(SmokeHostInputs inputs)
    {
        using var core = new Rhino.Runtime.InProcess.RhinoCore(CoreArguments);
        if (Rhino.RhinoApp.Version.Major != 7)
        {
            throw new InvalidOperationException("Rhino 7 runtime was not loaded.");
        }

        GrasshopperSmokeGate.RestrictExternalLibraries(inputs.PluginPaths);
        object grasshopper = Rhino.RhinoApp.GetPlugInObject("Grasshopper")
            ?? throw new InvalidOperationException(
                "The installed Grasshopper plug-in could not be loaded by Rhino 7.");
        System.Reflection.MethodInfo method = grasshopper.GetType().GetMethod(
            "RunHeadless",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?? throw new MissingMethodException(grasshopper.GetType().FullName, "RunHeadless");
        method.Invoke(grasshopper, null);

        GrasshopperSmokeSummary summary = GrasshopperSmokeGate.Run(
            inputs,
            "Rhino7",
            Rhino.RhinoApp.Version.ToString());
        Console.WriteLine(summary.ToConsoleText());
        return 0;
    }
}
