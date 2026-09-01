using RhinoInside;

namespace Dragons.GrasshopperSmoke.Rhino8;

internal static class Program
{
    private static readonly string[] CoreArguments = { "/netcore" };

    static Program()
    {
        Resolver.Initialize(Rhino8Environment.ResolveRhinoSystemDirectory());
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            SmokeHostInputs inputs = SmokeHostInputs.FromEnvironment();
            Directory.CreateDirectory(inputs.OutputDirectory);
            return RhinoHost.Run(inputs, CoreArguments);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Dragons Grasshopper Rhino 8 host gate failed.");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

}
