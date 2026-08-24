using System.Reflection;

namespace GonieGonie.Dragons.ExampleDefinitions.Rhino7;

internal static class Rhino7Host
{
    private static readonly string[] CoreArguments = { "/nosplash", "/notemplate" };

    internal static int Run(ExampleHostInputs inputs)
    {
        using var core = new Rhino.Runtime.InProcess.RhinoCore(CoreArguments);
        if (Rhino.RhinoApp.Version.Major != 7)
        {
            throw new InvalidOperationException("Rhino 7 runtime was not loaded.");
        }

        ExampleDefinitionGate.RestrictExternalLibraries(inputs.PluginPaths);
        object grasshopper = Rhino.RhinoApp.GetPlugInObject("Grasshopper")
            ?? throw new InvalidOperationException("The installed Grasshopper plug-in could not be loaded.");
        MethodInfo method = grasshopper.GetType().GetMethod(
            "RunHeadless",
            BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(grasshopper.GetType().FullName, "RunHeadless");
        method.Invoke(grasshopper, null);
        ExampleDefinitionGate.Run(inputs, "Rhino7", Rhino.RhinoApp.Version.ToString());
        return 0;
    }
}
