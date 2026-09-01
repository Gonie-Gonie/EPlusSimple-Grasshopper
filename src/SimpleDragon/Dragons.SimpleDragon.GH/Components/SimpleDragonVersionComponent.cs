using Grasshopper.Kernel;

namespace Dragons.SimpleDragon.Grasshopper.Components;

public sealed class SimpleDragonVersionComponent : GH_Component
{
    public SimpleDragonVersionComponent()
        : base(
            "SimpleDragon Version",
            "SimpleDragonVersion",
            "Reports the SimpleDragon port and tracked upstream versions.",
            "SimpleDragon",
            "Core")
    {
    }

    public override Guid ComponentGuid => new("ea29f1c8-72aa-446a-8da4-c786ab470237");

    protected override System.Drawing.Bitmap? Icon => PluginIcons.ForComponent(GetType());

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Version", "V", "SimpleDragon.GH version.", GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Upstream",
            "U",
            "Tracked SimpleDragon upstream commit.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        DA.SetData(0, PackageInfo.Version);
        DA.SetData(1, PackageInfo.Compatibility.UpstreamCommit);
    }
}
