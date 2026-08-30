using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

public sealed class SimpleDragonPackagedAirConditionerComponent : SimpleDragonHvacComponent
{
    public SimpleDragonPackagedAirConditionerComponent()
        : base(
            "SimpleDragon Packaged Air Conditioner",
            "SD Packaged AC",
            "Creates a cooling-only packaged air conditioner. Capacity is watts; disconnected capacity remains autosized/unset.")
    {
    }

    public override Guid ComponentGuid => new("8b4b8f93-cd03-4bd2-a7fa-da20bd802946");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Packaged-air-conditioner name.", GH_ParamAccess.item, "Packaged Air Conditioner");
        pManager.AddNumberParameter("Cooling COP", "COP", "Dimensionless cooling COP (> 0).", GH_ParamAccess.item, 3d);
        pManager.AddNumberParameter("Cooling Capacity", "Cap", "Optional cooling capacity in W; leave disconnected for autosize/unset.", GH_ParamAccess.item);
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
        pManager[2].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        AddOutputs(pManager, "Authored packaged air conditioner.");
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Packaged Air Conditioner";
        double cop = 3d;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) || !DA.GetData(1, ref cop))
        {
            return;
        }

        double? capacity = OptionalNumber(DA, 2);
        DA.GetData(3, ref id);
        Author(
            DA,
            1,
            "SD.GH.HVAC.PACKAGED_INVALID",
            "Use a positive cooling COP/capacity and a non-empty name.",
            () =>
            {
                var supply = new SupplySystem(
                    name,
                    SupplySystemType.PackagedAirConditioner,
                    coolingCop: cop,
                    coolingCapacity: capacity,
                    id: OptionalId(id));
                DA.SetData(0, new SimpleDragonSupplySystemGoo(supply));
            });
    }

    private static void AddOutputs(GH_OutputParamManager manager, string description)
    {
        manager.AddParameter(new SimpleDragonSupplySystemParam(), "Supply", "S", description, GH_ParamAccess.item);
        manager.AddParameter(new SimpleDragonDiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }
}

public abstract class SimpleDragonSourceSupplyComponent : SimpleDragonHvacComponent
{
    protected SimpleDragonSourceSupplyComponent(
        string name,
        string nickname,
        string description)
        : base(name, nickname, description)
    {
    }

    protected abstract SupplySystemType SupplyType { get; }

    protected abstract string DefaultName { get; }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Supply-system name.", GH_ParamAccess.item, DefaultName);
        pManager.AddParameter(new SimpleDragonSourceSystemParam(), "Source", "Src", "Required compatible SimpleDragon source system.", GH_ParamAccess.item);
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonSupplySystemParam(), "Supply", "S", "Authored SimpleDragon supply system.", GH_ParamAccess.item);
        pManager.AddParameter(new SimpleDragonDiagnosticParam(), "Diagnostics", "D", "Compatibility and authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = DefaultName;
        SimpleDragonSourceSystemGoo? sourceGoo = null;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) || !DA.GetData(1, ref sourceGoo))
        {
            return;
        }

        DA.GetData(2, ref id);
        Author(
            DA,
            1,
            "SD.GH.HVAC.SOURCE_INCOMPATIBLE",
            "Connect one of the source types listed in the component input description.",
            () =>
            {
                SourceSystem source = Value<SimpleDragonSourceSystemGoo, SourceSystem>(sourceGoo, "Source");
                EnsureCompatible(SupplyType, source);
                var supply = new SupplySystem(
                    name,
                    SupplyType,
                    source.Id.Value,
                    source,
                    id: OptionalId(id));
                DA.SetData(0, new SimpleDragonSupplySystemGoo(supply));
            });
    }
}

public sealed class SimpleDragonAirHandlingUnitComponent : SimpleDragonSourceSupplyComponent
{
    public SimpleDragonAirHandlingUnitComponent()
        : base(
            "SimpleDragon Air Handling Unit",
            "SD AHU",
            "Creates an air handling unit. Compatible sources: Heat Pump or Geothermal Heat Pump.")
    {
    }

    protected override SupplySystemType SupplyType => SupplySystemType.AirHandlingUnit;

    protected override string DefaultName => "Air Handling Unit";

    public override Guid ComponentGuid => new("8b0839fc-d03d-46af-8897-1ba4a41eab46");
}

public sealed class SimpleDragonFanCoilUnitComponent : SimpleDragonSourceSupplyComponent
{
    public SimpleDragonFanCoilUnitComponent()
        : base(
            "SimpleDragon Fan Coil Unit",
            "SD Fan Coil",
            "Creates a fan coil unit. Compatible sources: Boiler, District Heating, Chiller, or Absorption Chiller.")
    {
    }

    protected override SupplySystemType SupplyType => SupplySystemType.FanCoilUnit;

    protected override string DefaultName => "Fan Coil Unit";

    public override Guid ComponentGuid => new("dd41df8f-9e3e-4663-8ce7-89025cfde30c");
}

public sealed class SimpleDragonRadiatorComponent : SimpleDragonHvacComponent
{
    public SimpleDragonRadiatorComponent()
        : base(
            "SimpleDragon Radiator",
            "SD Radiator",
            "Creates a hydronic radiator. Compatible sources: Boiler or District Heating. Capacity is watts.")
    {
    }

    public override Guid ComponentGuid => new("2e77eee2-c354-40ba-abae-b501373046bc");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Radiator name.", GH_ParamAccess.item, "Radiator");
        pManager.AddParameter(new SimpleDragonSourceSystemParam(), "Source", "Src", "Required Boiler or District Heating source.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Heating Capacity", "Cap", "Optional heating capacity in W; leave disconnected for autosize/unset.", GH_ParamAccess.item);
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
        pManager[2].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonSupplySystemParam(), "Supply", "S", "Authored hydronic radiator.", GH_ParamAccess.item);
        pManager.AddParameter(new SimpleDragonDiagnosticParam(), "Diagnostics", "D", "Compatibility and authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Radiator";
        SimpleDragonSourceSystemGoo? sourceGoo = null;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) || !DA.GetData(1, ref sourceGoo))
        {
            return;
        }

        double? capacity = OptionalNumber(DA, 2);
        DA.GetData(3, ref id);
        Author(
            DA,
            1,
            "SD.GH.HVAC.SOURCE_INCOMPATIBLE",
            "Connect a Boiler or District Heating source and use a positive optional capacity.",
            () =>
            {
                SourceSystem source = Value<SimpleDragonSourceSystemGoo, SourceSystem>(sourceGoo, "Source");
                EnsureCompatible(SupplySystemType.Radiator, source);
                var supply = new SupplySystem(
                    name,
                    SupplySystemType.Radiator,
                    source.Id.Value,
                    source,
                    heatingCapacity: capacity,
                    id: OptionalId(id));
                DA.SetData(0, new SimpleDragonSupplySystemGoo(supply));
            });
    }
}

public sealed class SimpleDragonElectricRadiatorComponent : SimpleDragonHvacComponent
{
    public SimpleDragonElectricRadiatorComponent()
        : base(
            "SimpleDragon Electric Radiator",
            "SD Electric Radiator",
            "Creates a source-free electric radiator. Capacity is watts; disconnected capacity remains autosized/unset.")
    {
    }

    public override Guid ComponentGuid => new("3a3f5157-23bb-4094-83fd-e5cf4dc4d891");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Electric-radiator name.", GH_ParamAccess.item, "Electric Radiator");
        pManager.AddNumberParameter("Heating Capacity", "Cap", "Optional heating capacity in W; leave disconnected for autosize/unset.", GH_ParamAccess.item);
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
        pManager[1].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonSupplySystemParam(), "Supply", "S", "Authored electric radiator.", GH_ParamAccess.item);
        pManager.AddParameter(new SimpleDragonDiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Electric Radiator";
        string id = string.Empty;
        if (!DA.GetData(0, ref name))
        {
            return;
        }

        double? capacity = OptionalNumber(DA, 1);
        DA.GetData(2, ref id);
        Author(
            DA,
            1,
            "SD.GH.HVAC.ELECTRIC_RADIATOR_INVALID",
            "Use a non-empty name and a positive optional heating capacity.",
            () =>
            {
                var supply = new SupplySystem(
                    name,
                    SupplySystemType.ElectricRadiator,
                    heatingCapacity: capacity,
                    id: OptionalId(id));
                DA.SetData(0, new SimpleDragonSupplySystemGoo(supply));
            });
    }
}

public sealed class SimpleDragonRadiantFloorComponent : SimpleDragonSourceSupplyComponent
{
    public SimpleDragonRadiantFloorComponent()
        : base(
            "SimpleDragon Radiant Floor",
            "SD Radiant Floor",
            "Creates a hydronic radiant floor. Compatible sources: Boiler or District Heating.")
    {
    }

    protected override SupplySystemType SupplyType => SupplySystemType.RadiantFloor;

    protected override string DefaultName => "Radiant Floor";

    public override Guid ComponentGuid => new("c1315d1b-457b-444c-bda9-05aaa6a17749");
}

public sealed class SimpleDragonElectricRadiantFloorComponent : SimpleDragonHvacComponent
{
    public SimpleDragonElectricRadiantFloorComponent()
        : base(
            "SimpleDragon Electric Radiant Floor",
            "SD Electric Floor",
            "Creates a source-free electric radiant-floor supply system.")
    {
    }

    public override Guid ComponentGuid => new("e7d20017-8999-4cc1-bc12-f288f3f2ceb7");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Electric-radiant-floor name.", GH_ParamAccess.item, "Electric Radiant Floor");
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonSupplySystemParam(), "Supply", "S", "Authored electric radiant floor.", GH_ParamAccess.item);
        pManager.AddParameter(new SimpleDragonDiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Electric Radiant Floor";
        string id = string.Empty;
        if (!DA.GetData(0, ref name))
        {
            return;
        }

        DA.GetData(1, ref id);
        Author(
            DA,
            1,
            "SD.GH.HVAC.ELECTRIC_FLOOR_INVALID",
            "Use a non-empty name and a valid optional ID.",
            () =>
            {
                var supply = new SupplySystem(
                    name,
                    SupplySystemType.ElectricRadiantFloor,
                    id: OptionalId(id));
                DA.SetData(0, new SimpleDragonSupplySystemGoo(supply));
            });
    }
}
