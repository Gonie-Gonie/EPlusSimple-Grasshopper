using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper;

public sealed class SimpleDragonAssemblyInfo : GH_AssemblyInfo
{
    public override string Name => "SimpleDragon";

    public override System.Drawing.Bitmap? Icon => PluginIcons.Icon24;

    public override string Description =>
        "Area-and-azimuth SimpleDragon workflows for Grasshopper.";

    public override Guid Id => new("7cbbbe1e-d913-4a89-9607-1e9254bf26d8");

    public override string AuthorName => "Gonie-Gonie";

    public override string AuthorContact => "https://github.com/Gonie-Gonie/EPlusSimple-Grasshopper";
}
