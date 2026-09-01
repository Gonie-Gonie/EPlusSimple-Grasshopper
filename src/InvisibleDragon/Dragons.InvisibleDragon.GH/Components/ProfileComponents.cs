using Grasshopper.Kernel;
using Dragons.InvisibleDragon.Grasshopper.Parameters;
using Dragons.InvisibleDragon.Grasshopper.Types;
using Dragons.InvisibleDragon.Profile;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

namespace Dragons.InvisibleDragon.Grasshopper.Components;

public sealed class ConstantProfileComponent : DragonComponent
{
    public ConstantProfileComponent()
        : base(
            "Constant Profile",
            "Prof",
            "Creates a basic annual zone profile with constant setpoints and occupancy.",
            DragonPanels.Profile)
    {
    }

    public override Guid ComponentGuid => new("3d5717de-1b16-406a-91e0-7a392c08aa51");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Profile name.", GH_ParamAccess.item, "Basic Profile");
        pManager.AddNumberParameter("Heating Setpoint", "Heat", "Constant heating setpoint in °C.", GH_ParamAccess.item, 20);
        pManager.AddNumberParameter("Cooling Setpoint", "Cool", "Constant cooling setpoint in °C.", GH_ParamAccess.item, 26);
        pManager.AddNumberParameter("Occupancy", "Occ", "Constant non-negative occupant schedule value.", GH_ParamAccess.item, 0);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonProfileParam(), "Profile", "P", "InvisibleDragon zone profile.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Profile validation diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Basic Profile";
        double heating = 20;
        double cooling = 26;
        double occupancy = 0;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref heating) ||
            !DA.GetData(2, ref cooling) ||
            !DA.GetData(3, ref occupancy))
        {
            return;
        }

        var profile = new ZoneProfile(
            StableIds.Create("profile", name),
            name,
            Schedule.Constant($"{name}:Heating", heating, ScheduleType.Temperature),
            Schedule.Constant($"{name}:Cooling", cooling, ScheduleType.Temperature),
            Schedule.Constant($"{name}:HVAC", 1, ScheduleType.OnOff),
            Schedule.Constant($"{name}:Occupant", occupancy, ScheduleType.Real));
        var diagnostics = profile.Validate().Diagnostics;
        Report(diagnostics);
        DA.SetData(0, new DragonProfileGoo(profile));
        DA.SetDataList(1, diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}
