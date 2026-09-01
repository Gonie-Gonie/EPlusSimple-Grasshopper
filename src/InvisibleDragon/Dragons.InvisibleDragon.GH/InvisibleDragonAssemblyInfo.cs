using Grasshopper.Kernel;

namespace Dragons.InvisibleDragon.Grasshopper;

public sealed class InvisibleDragonAssemblyInfo : GH_AssemblyInfo
{
    public override string Name => "InvisibleDragon";

    public override System.Drawing.Bitmap? Icon => PluginIcons.Icon24;

    public override string Description =>
        "Vertex-preserving EnergyPlus model construction for Grasshopper.";

    public override Guid Id => new("e2b0359b-c01c-4e31-a5f3-b7e058f1e0cc");

    public override string AuthorName => "Gonie-Gonie";

    public override string AuthorContact => "https://github.com/Gonie-Gonie/EPlusSimple-Grasshopper";
}
