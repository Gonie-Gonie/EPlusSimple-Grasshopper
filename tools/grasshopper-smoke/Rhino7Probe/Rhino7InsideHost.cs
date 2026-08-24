namespace GonieGonie.Dragons.Grasshopper.Rhino7Probe;

internal static class Rhino7InsideHost
{
    private static readonly string[] CoreArguments = { "/nosplash", "/notemplate" };

    public static int Run(string invisibleGha, string simpleGha, string outputPath)
    {
        using var core = new Rhino.Runtime.InProcess.RhinoCore(CoreArguments);
        SimpleDragonRhino7Smoke.Run();
        return Rhino7Gate.RunHosted(invisibleGha, simpleGha, outputPath);
    }
}
