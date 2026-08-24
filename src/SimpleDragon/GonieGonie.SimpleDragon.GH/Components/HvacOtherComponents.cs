using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

public sealed class SimpleDragonEnergyRecoveryVentilatorComponent : SimpleDragonHvacComponent
{
    public SimpleDragonEnergyRecoveryVentilatorComponent()
        : base(
            "SimpleDragon Energy Recovery Ventilator",
            "SD ERV",
            "Creates a SimpleDragon energy-recovery ventilator. Airflow is m³/s and efficiencies are fractions.")
    {
    }

    public override Guid ComponentGuid => new("15afd6e6-1c05-4715-909b-b6e98ef91375");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Ventilator name.", GH_ParamAccess.item, "Energy Recovery Ventilator");
        pManager.AddNumberParameter("Airflow", "Flow", "Design supply airflow rate in m³/s (> 0).", GH_ParamAccess.item, 0.2d);
        pManager.AddNumberParameter("Heating Efficiency", "HEff", "Sensible heating-recovery efficiency fraction in (0, 1).", GH_ParamAccess.item, 0.7d);
        pManager.AddNumberParameter("Cooling Efficiency", "CEff", "Cooling-recovery efficiency fraction in (0, 1).", GH_ParamAccess.item, 0.45d);
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonEnergyRecoveryVentilatorParam(), "ERV", "ERV", "Authored energy-recovery ventilator.", GH_ParamAccess.item);
        pManager.AddParameter(new GonieGonie.InvisibleDragon.Grasshopper.Parameters.DiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Energy Recovery Ventilator";
        double airflow = 0.2d;
        double heatingEfficiency = 0.7d;
        double coolingEfficiency = 0.45d;
        string id = string.Empty;
        if (!DA.GetData(0, ref name)
            || !DA.GetData(1, ref airflow)
            || !DA.GetData(2, ref heatingEfficiency)
            || !DA.GetData(3, ref coolingEfficiency))
        {
            return;
        }

        DA.GetData(4, ref id);
        Author(
            DA,
            1,
            "SD.GH.HVAC.ERV_INVALID",
            "Use positive airflow and heating/cooling efficiency fractions strictly between 0 and 1.",
            () =>
            {
                var ventilator = new VentilationSystem(
                    name,
                    airflow,
                    heatingEfficiency,
                    coolingEfficiency,
                    OptionalId(id));
                DA.SetData(0, new SimpleDragonEnergyRecoveryVentilatorGoo(ventilator));
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
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonPhotovoltaicPanelParam(), "PV", "PV", "Authored photovoltaic panel.", GH_ParamAccess.item);
        pManager.AddParameter(new GonieGonie.InvisibleDragon.Grasshopper.Parameters.DiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Photovoltaic Panel";
        double area = 10d;
        double efficiency = 0.2d;
        double azimuth = 180d;
        double tilt = 30d;
        string id = string.Empty;
        if (!DA.GetData(0, ref name)
            || !DA.GetData(1, ref area)
            || !DA.GetData(2, ref efficiency)
            || !DA.GetData(3, ref azimuth)
            || !DA.GetData(4, ref tilt))
        {
            return;
        }

        DA.GetData(5, ref id);
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
                    tilt,
                    OptionalId(id));
                DA.SetData(0, new SimpleDragonPhotovoltaicPanelGoo(panel));
            });
    }
}

public sealed class AssignSimpleDragonVentilationSystemsComponent : SimpleDragonHvacComponent
{
    public AssignSimpleDragonVentilationSystemsComponent()
        : base(
            "Assign SimpleDragon Ventilation Systems",
            "Assign SD ERVs",
            "Returns a new immutable zone with ERV assignments. Counts may be omitted (all 1), broadcast from one value, or supplied one per ERV.")
    {
    }

    public override Guid ComponentGuid => new("5f66b3fd-e69c-4c33-92db-839c07dcbda5");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonZoneParam(), "Zone", "Z", "Zone to copy and update.", GH_ParamAccess.item);
        pManager.AddParameter(new SimpleDragonEnergyRecoveryVentilatorParam(), "ERVs", "ERV", "Energy-recovery ventilators to assign.", GH_ParamAccess.list);
        pManager.AddIntegerParameter("Counts", "Count", "Optional unit counts: omit for 1 each, supply one value to broadcast, or one per ERV.", GH_ParamAccess.list);
        pManager.AddBooleanParameter("Replace Existing", "Replace", "True replaces all existing ventilation assignments; false appends and rejects duplicate IDs.", GH_ParamAccess.item, false);
        pManager[2].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonZoneParam(), "Zone", "Z", "Updated immutable zone.", GH_ParamAccess.item);
        pManager.AddParameter(new GonieGonie.InvisibleDragon.Grasshopper.Parameters.DiagnosticParam(), "Diagnostics", "D", "Assignment diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        SimpleDragonZoneGoo? zoneGoo = null;
        var ventilatorGoos = new List<SimpleDragonEnergyRecoveryVentilatorGoo>();
        var counts = new List<int>();
        bool replace = false;
        if (!DA.GetData(0, ref zoneGoo)
            || !DA.GetDataList(1, ventilatorGoos)
            || !DA.GetData(3, ref replace))
        {
            return;
        }

        DA.GetDataList(2, counts);
        Author(
            DA,
            1,
            "SD.GH.HVAC.ASSIGN_VENTILATION_INVALID",
            "Use positive counts and provide either zero, one, or one count per ERV; remove duplicate ventilation IDs.",
            () =>
            {
                Zone zone = Value<SimpleDragonZoneGoo, Zone>(zoneGoo, "Zone");
                VentilationSystem[] ventilators = ventilatorGoos.Select((goo, index) =>
                    Value<SimpleDragonEnergyRecoveryVentilatorGoo, VentilationSystem>(
                        goo,
                        "ERVs[" + index + "]"))
                    .ToArray();
                int[] resolvedCounts = ResolveCounts(counts, ventilators.Length);
                IEnumerable<VentilationAssignment> assignments = replace
                    ? Array.Empty<VentilationAssignment>()
                    : zone.VentilationAssignments;
                assignments = assignments.Concat(ventilators.Select((item, index) =>
                    new VentilationAssignment(item.Id.Value, resolvedCounts[index], item)));
                var updated = new Zone(
                    zone.Name,
                    zone.FloorNumber,
                    zone.Height,
                    zone.Surfaces,
                    zone.ProfileName,
                    zone.Profile,
                    zone.LightDensity,
                    zone.SupplySystemAssignments,
                    assignments,
                    zone.Id);
                DA.SetData(0, new SimpleDragonZoneGoo(updated));
            });
    }

    private static int[] ResolveCounts(IReadOnlyList<int> counts, int ventilatorCount)
    {
        if (counts.Count == 0)
        {
            return Enumerable.Repeat(1, ventilatorCount).ToArray();
        }

        if (counts.Count == 1)
        {
            return Enumerable.Repeat(counts[0], ventilatorCount).ToArray();
        }

        if (counts.Count != ventilatorCount)
        {
            throw new ArgumentException(
                "Counts must be omitted, contain one broadcast value, or contain exactly one value per ERV.",
                nameof(counts));
        }

        return counts.ToArray();
    }
}
