using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Shape;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

public sealed class EnergyRecoveryVentilatorComponent : DragonComponent
{
    public EnergyRecoveryVentilatorComponent()
        : base(
            "Energy Recovery Ventilator",
            "ERV",
            "Creates a sensible-and-latent energy recovery ventilator for zone assignment.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("3d5f630e-66c3-43da-b73c-50d5be1792c3");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Ventilator name.", GH_ParamAccess.item, "Energy Recovery Ventilator");
        pManager.AddNumberParameter("Sensible Effectiveness", "Sens", "Sensible heat-recovery effectiveness from 0 to 1.", GH_ParamAccess.item, 0.75);
        pManager.AddNumberParameter("Latent Effectiveness", "Lat", "Latent heat-recovery effectiveness from 0 to 1.", GH_ParamAccess.item, 0.65);
        pManager.AddNumberParameter("Supply Air Flow", "Flow", "Supply air flow in m³/s; 0 means autosize.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Fan Total Efficiency", "FanEff", "Supply and exhaust fan total efficiency from 0 to 1.", GH_ParamAccess.item, 0.7);
        pManager.AddNumberParameter("Fan Pressure Rise", "dP", "Supply and exhaust fan pressure rise in Pa.", GH_ParamAccess.item, 100.0);
        pManager.AddTextParameter("ID", "ID", "Optional stable ventilator identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonEnergyRecoveryVentilatorParam(),
            "Ventilator",
            "V",
            "InvisibleDragon energy recovery ventilator.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Energy Recovery Ventilator";
        double sensible = 0.75;
        double latent = 0.65;
        double flow = 0;
        double fanEfficiency = 0.7;
        double pressureRise = 100;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref sensible) ||
            !DA.GetData(2, ref latent) ||
            !DA.GetData(3, ref flow) ||
            !DA.GetData(4, ref fanEfficiency) ||
            !DA.GetData(5, ref pressureRise))
        {
            return;
        }

        DA.GetData(6, ref id);
        var ventilator = new EnergyRecoveryVentilator(
            StableIds.Resolve(id, "energy-recovery-ventilator", name),
            name,
            sensible,
            latent,
            HvacComponentSupport.OptionalPositive(flow, "Supply Air Flow"),
            fanEfficiency,
            pressureRise);
        DA.SetData(0, new DragonEnergyRecoveryVentilatorGoo(ventilator));
    }
}

public sealed class PhotovoltaicPanelComponent : DragonComponent
{
    public PhotovoltaicPanelComponent()
        : base(
            "Photovoltaic Panel",
            "PV",
            "Creates a fixed-geometry photovoltaic panel and simple conversion performance.",
            "Systems")
    {
    }

    public override Guid ComponentGuid => new("237bc85d-769a-468b-a048-70e3b5c382ee");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Photovoltaic panel name.", GH_ParamAccess.item, "Photovoltaic Panel");
        pManager.AddNumberParameter("Area", "A", "Gross panel area in m².", GH_ParamAccess.item, 10.0);
        pManager.AddNumberParameter("Tilt", "Tilt", "Panel tilt in degrees from horizontal, 0 to 90.", GH_ParamAccess.item, 30.0);
        pManager.AddNumberParameter("Azimuth", "Az", "Panel azimuth in degrees clockwise from north, 0 to less than 360.", GH_ParamAccess.item, 180.0);
        pManager.AddNumberParameter("Efficiency", "Eff", "Module conversion efficiency from 0 to 1.", GH_ParamAccess.item, 0.2);
        pManager.AddNumberParameter("Active Cell Area Fraction", "Cell", "Fraction of gross area occupied by active cells, from 0 to 1.", GH_ParamAccess.item, 0.7);
        pManager.AddTextParameter("ID", "ID", "Optional stable photovoltaic-panel identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonPhotovoltaicPanelParam(),
            "PV Panel",
            "PV",
            "InvisibleDragon photovoltaic panel.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Photovoltaic Panel";
        double area = 10;
        double tilt = 30;
        double azimuth = 180;
        double efficiency = 0.2;
        double activeCellFraction = 0.7;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref area) ||
            !DA.GetData(2, ref tilt) ||
            !DA.GetData(3, ref azimuth) ||
            !DA.GetData(4, ref efficiency) ||
            !DA.GetData(5, ref activeCellFraction))
        {
            return;
        }

        DA.GetData(6, ref id);
        var panel = new PhotovoltaicPanel(
            StableIds.Resolve(id, "photovoltaic-panel", name),
            name,
            area,
            tilt,
            azimuth,
            efficiency,
            activeCellFraction);
        DA.SetData(0, new DragonPhotovoltaicPanelGoo(panel));
    }
}

public sealed class SupplyGroupAssignmentComponent : DragonComponent
{
    public SupplyGroupAssignmentComponent()
        : base(
            "Supply Group Assignment",
            "AssignHVAC",
            "Groups zone supply systems with optional availability schedules and assigns them to a thermal zone.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("1c78fc6e-952f-4513-a39f-b107daba9677");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new DragonZoneParam(), "Zone", "Z", "Thermal zone receiving this supply group.", GH_ParamAccess.item);
        pManager.AddParameter(new DragonSupplySystemParam(), "Supply Systems", "S", "Ordered heating and cooling supply systems.", GH_ParamAccess.list);
        int availability = pManager.AddParameter(
            new DragonScheduleParam(),
            "Availability Schedules",
            "A",
            "Optional OnOff schedules: empty uses zone/default availability, one broadcasts, or provide one per system.",
            GH_ParamAccess.list);
        pManager[availability].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddGenericParameter("Supply Group", "G", "SupplyGroup domain value.", GH_ParamAccess.item);
        pManager.AddGenericParameter("HVAC Assignment", "A", "ZoneHvacAssignment domain value for Energy Model.", GH_ParamAccess.item);
        pManager.AddParameter(new DragonSourceSystemParam(), "Sources", "Src", "Distinct source systems referenced by the group.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        DragonZoneGoo? zoneGoo = null;
        var supplyGoos = new List<DragonSupplySystemGoo>();
        var availabilityGoos = new List<DragonScheduleGoo>();
        if (!DA.GetData(0, ref zoneGoo) || !DA.GetDataList(1, supplyGoos))
        {
            return;
        }

        DA.GetDataList(2, availabilityGoos);
        Zone zone = zoneGoo?.Value
            ?? throw new ArgumentException("Zone requires a non-empty thermal-zone value.");
        SupplySystem[] systems = supplyGoos
            .Select((goo, index) => HvacComponentSupport.Supply(goo, "Supply Systems", index))
            .ToArray();
        var group = new SupplyGroup(
            systems,
            HvacComponentSupport.AvailabilitySchedules(availabilityGoos, systems.Length));
        var assignment = new ZoneHvacAssignment(zone.Id, group);
        DA.SetData(0, new GH_ObjectWrapper(group));
        DA.SetData(1, new GH_ObjectWrapper(assignment));
        DA.SetDataList(2, group.Sources.Select(source => new DragonSourceSystemGoo(source)));
    }
}
