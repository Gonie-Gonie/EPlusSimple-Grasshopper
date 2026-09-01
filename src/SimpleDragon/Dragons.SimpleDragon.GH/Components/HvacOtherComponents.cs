using Dragons.SimpleDragon.Grasshopper.Parameters;
using Dragons.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace Dragons.SimpleDragon.Grasshopper.Components;

public sealed class SimpleDragonEnergyRecoveryVentilatorComponent : SimpleDragonHvacComponent
{
    public SimpleDragonEnergyRecoveryVentilatorComponent()
        : base(
            "SimpleDragon Energy Recovery Ventilator",
            "SD ERV",
            "Creates an ERV owned by the SimpleDragon Zone it is connected to. Airflow is m³/s and efficiencies are fractions.")
    {
    }

    public override Guid ComponentGuid => new("15afd6e6-1c05-4715-909b-b6e98ef91375");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Ventilator name.", GH_ParamAccess.item, "Energy Recovery Ventilator");
        pManager.AddNumberParameter("Airflow", "Flow", "Design supply airflow rate in m³/s (> 0).", GH_ParamAccess.item, 0.2d);
        pManager.AddNumberParameter("Heating Efficiency", "HEff", "Sensible heating-recovery efficiency fraction in (0, 1).", GH_ParamAccess.item, 0.7d);
        pManager.AddNumberParameter("Cooling Efficiency", "CEff", "Cooling-recovery efficiency fraction in (0, 1).", GH_ParamAccess.item, 0.45d);
        int count = pManager.AddIntegerParameter(
            "Count",
            "Count",
            "Optional positive number of identical ERV units owned by the connected Zone.",
            GH_ParamAccess.item,
            1);
        pManager[count].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new SimpleDragonZoneErvParam(),
            "Zone ERV",
            "ERV",
            "Owned ERV value to connect directly to one SimpleDragon Zone.",
            GH_ParamAccess.item);
        pManager.AddParameter(new SimpleDragonDiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Energy Recovery Ventilator";
        double airflow = 0.2d;
        double heatingEfficiency = 0.7d;
        double coolingEfficiency = 0.45d;
        int count = 1;
        if (!DA.GetData(0, ref name)
            || !DA.GetData(1, ref airflow)
            || !DA.GetData(2, ref heatingEfficiency)
            || !DA.GetData(3, ref coolingEfficiency))
        {
            return;
        }

        DA.GetData(4, ref count);
        Author(
            DA,
            1,
            "SD.GH.HVAC.ERV_INVALID",
            "Use positive airflow and Count values and heating/cooling efficiency fractions strictly between 0 and 1.",
            () =>
            {
                var ventilator = new VentilationSystem(
                    name,
                    airflow,
                    heatingEfficiency,
                    coolingEfficiency);
                var ownedErv = new VentilationAssignment(ventilator.Id.Value, count, ventilator);
                DA.SetData(0, new SimpleDragonZoneErvGoo(ownedErv));
            });
    }
}

public sealed class SimpleDragonPhotovoltaicPanelComponent : SimpleDragonHvacComponent
{
    public SimpleDragonPhotovoltaicPanelComponent()
        : base(
            "SimpleDragon Photovoltaic Panel",
            "SD PV",
            "Creates a SimpleDragon photovoltaic panel. Area is m²; azimuth is clockwise from north; tilt is degrees above horizontal.")
    {
    }

    public override Guid ComponentGuid => new("7fcb5c47-3d49-4aa0-8fbc-bd765711401f");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Photovoltaic-panel name.", GH_ParamAccess.item, "Photovoltaic Panel");
        pManager.AddNumberParameter("Area", "A", "Active panel area in m² (> 0).", GH_ParamAccess.item, 10d);
        pManager.AddNumberParameter("Efficiency", "Eff", "Conversion efficiency fraction in (0, 1].", GH_ParamAccess.item, 0.2d);
        pManager.AddNumberParameter("Azimuth", "Az", "Clockwise azimuth from north in degrees [0, 360).", GH_ParamAccess.item, 180d);
        pManager.AddNumberParameter("Tilt", "Tilt", "Tilt above horizontal in degrees [0, 90].", GH_ParamAccess.item, 30d);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonPhotovoltaicPanelParam(), "PV", "PV", "Authored photovoltaic panel.", GH_ParamAccess.item);
        pManager.AddParameter(new SimpleDragonDiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Photovoltaic Panel";
        double area = 10d;
        double efficiency = 0.2d;
        double azimuth = 180d;
        double tilt = 30d;
        if (!DA.GetData(0, ref name)
            || !DA.GetData(1, ref area)
            || !DA.GetData(2, ref efficiency)
            || !DA.GetData(3, ref azimuth)
            || !DA.GetData(4, ref tilt))
        {
            return;
        }

        Author(
            DA,
            1,
            "SD.GH.HVAC.PV_INVALID",
            "Use positive area, efficiency in (0, 1], azimuth in [0, 360), and tilt in [0, 90].",
            () =>
            {
                var panel = new PhotovoltaicSystem(
                    name,
                    area,
                    efficiency,
                    azimuth,
                    tilt);
                DA.SetData(0, new SimpleDragonPhotovoltaicPanelGoo(panel));
            });
    }
}
