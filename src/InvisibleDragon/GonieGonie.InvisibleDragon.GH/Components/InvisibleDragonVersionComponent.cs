using Grasshopper.Kernel;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

public sealed class InvisibleDragonVersionComponent : GH_Component
{
    public InvisibleDragonVersionComponent()
        : base(
            "InvisibleDragon Version",
            "InvisibleDragonVersion",
            "Reports the InvisibleDragon port and tracked upstream versions.",
            "InvisibleDragon",
            "Core")
    {
    }

    public override Guid ComponentGuid => new("bcdd73c6-e40f-4ae4-9b9b-5dc78a238b18");

    protected override System.Drawing.Bitmap? Icon => PluginIcons.ForComponent(GetType());

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Version", "V", "InvisibleDragon.GH version.", GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Upstream",
            "U",
            "Tracked upstream compatibility commit.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        DA.SetData(0, PackageInfo.Version);
        DA.SetData(1, PackageInfo.Compatibility.UpstreamCommit);
    }
}
