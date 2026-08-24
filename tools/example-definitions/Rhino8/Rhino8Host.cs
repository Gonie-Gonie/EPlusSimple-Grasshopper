using System.Reflection;

namespace GonieGonie.Dragons.ExampleDefinitions.Rhino8;

internal static class Rhino8Host
{
    private static readonly string[] CoreArguments = { "/netcore", "/nosplash", "/notemplate" };

    internal static int Run(ExampleHostInputs inputs)
    {
        using var core = new Rhino.Runtime.InProcess.RhinoCore(CoreArguments);
        if (Rhino.RhinoApp.Version.Major != 8)
        {
            throw new InvalidOperationException("Rhino 8 runtime was not loaded.");
        }

        using Rhino.RhinoDoc document = Rhino.RhinoDoc.Create(null)
            ?? throw new InvalidOperationException("Rhino 8 could not create a model document.");
        if (Rhino.RhinoDoc.ActiveDoc is null)
        {
            throw new InvalidOperationException("Rhino 8 did not activate the example model document.");
        }

        document.AdjustModelUnitSystem(Rhino.UnitSystem.Meters, scale: false);

        object grasshopper = Rhino.RhinoApp.GetPlugInObject("Grasshopper")
            ?? throw new InvalidOperationException("The installed Grasshopper plug-in could not be loaded.");
        ExampleDefinitionGate.RestrictExternalLibraries(inputs.PluginPaths);
        MethodInfo method = grasshopper.GetType().GetMethod(
            "RunHeadless",
            BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(grasshopper.GetType().FullName, "RunHeadless");
        method.Invoke(grasshopper, null);
        ExampleDefinitionGate.Run(inputs, "Rhino8", Rhino.RhinoApp.Version.ToString());
        return 0;
    }
}
