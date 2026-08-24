using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

public abstract class SimpleDragonHeatPumpSourceComponent : SimpleDragonHvacComponent
{
    protected SimpleDragonHeatPumpSourceComponent(
        string name,
        string nickname,
        string description)
        : base(name, nickname, description)
    {
    }

    protected abstract SourceSystemType SourceType { get; }

    protected abstract string DefaultName { get; }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Source-system name.", GH_ParamAccess.item, DefaultName);
        AddFuelParameter(pManager, "Energy carrier used by the heat pump.", FuelType.Electricity);
        pManager.AddNumberParameter("Heating COP", "HCOP", "Dimensionless heating coefficient of performance (> 0).", GH_ParamAccess.item, 3d);
        pManager.AddNumberParameter("Cooling COP", "CCOP", "Dimensionless cooling coefficient of performance (> 0).", GH_ParamAccess.item, 3d);
        pManager.AddNumberParameter("Heating Capacity", "HCap", "Optional nominal heating capacity in W; leave disconnected for autosize/unset.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Cooling Capacity", "CCap", "Optional nominal cooling capacity in W; leave disconnected for autosize/unset.", GH_ParamAccess.item);
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
        pManager[4].Optional = true;
        pManager[5].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonSourceSystemParam(), "Source", "S", "Authored SimpleDragon source system.", GH_ParamAccess.item);
        pManager.AddParameter(new GonieGonie.InvisibleDragon.Grasshopper.Parameters.DiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = DefaultName;
        int fuel = (int)FuelType.Electricity;
        double heatingCop = 3d;
        double coolingCop = 3d;
        string id = string.Empty;
        if (!DA.GetData(0, ref name)
            || !DA.GetData(1, ref fuel)
            || !DA.GetData(2, ref heatingCop)
            || !DA.GetData(3, ref coolingCop))
        {
            return;
        }

        double? heatingCapacity = OptionalNumber(DA, 4);
        double? coolingCapacity = OptionalNumber(DA, 5);
        DA.GetData(6, ref id);
        Author(
            DA,
            1,
            "SD.GH.HVAC.SOURCE_INVALID",
            "Use a listed fuel, positive COPs/capacities, and a non-empty valid name.",
            () =>
            {
                var source = new SourceSystem(
                    name,
                    SourceType,
                    EnumValue<FuelType>(fuel, "Fuel"),
                    heatingCop,
                    coolingCop,
                    heatingCapacity,
                    coolingCapacity,
                    id: OptionalId(id));
                DA.SetData(0, new SimpleDragonSourceSystemGoo(source));
            });
    }
}

public sealed class SimpleDragonHeatPumpComponent : SimpleDragonHeatPumpSourceComponent
{
    public SimpleDragonHeatPumpComponent()
        : base(
            "SimpleDragon Heat Pump",
            "SD Heat Pump",
            "Creates a reversible SimpleDragon heat-pump source. Capacities are watts; disconnected capacities remain autosized/unset.")
    {
    }

    protected override SourceSystemType SourceType => SourceSystemType.HeatPump;

    protected override string DefaultName => "Heat Pump";

    public override Guid ComponentGuid => new("e6e14d7b-55b4-45a9-97f9-9b99715f5ebc");
}

public sealed class SimpleDragonGeothermalHeatPumpComponent : SimpleDragonHeatPumpSourceComponent
{
    public SimpleDragonGeothermalHeatPumpComponent()
        : base(
            "SimpleDragon Geothermal Heat Pump",
            "SD Geothermal",
            "Creates a geothermal-identity SimpleDragon heat-pump source. Capacities are watts; disconnected capacities remain autosized/unset.")
    {
    }

    protected override SourceSystemType SourceType => SourceSystemType.GeothermalHeatPump;

    protected override string DefaultName => "Geothermal Heat Pump";

    public override Guid ComponentGuid => new("ebf437e1-425b-4cc5-a9db-c3e2276d2d8c");
}

public sealed class SimpleDragonChillerComponent : SimpleDragonHvacComponent
{
    public SimpleDragonChillerComponent()
        : base(
            "SimpleDragon Chiller",
            "SD Chiller",
            "Creates an electric chiller and cooling-tower definition. Capacities are watts and disconnected values remain autosized/unset.")
    {
    }

    public override Guid ComponentGuid => new("d5cedc15-8b76-49e3-842b-5b0c498556fd");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Chiller name.", GH_ParamAccess.item, "Chiller");
        pManager.AddNumberParameter("Cooling COP", "COP", "Dimensionless reference cooling COP (> 0).", GH_ParamAccess.item, 3d);
        pManager.AddNumberParameter("Cooling Capacity", "Cap", "Optional nominal cooling capacity in W; leave disconnected for autosize/unset.", GH_ParamAccess.item);
        AddEnumParameter(pManager, "Compressor", "Comp", "Compressor family: Turbo=0, Screw=1, Reciprocating=2.", CompressorType.Turbo);
        AddEnumParameter(pManager, "Tower Circuit", "Tower", "Cooling-tower circuit: Closed=0 or Open=1.", CoolingTowerType.Open);
        AddEnumParameter(pManager, "Tower Control", "Control", "Cooling-tower fan control: SingleSpeed=0 or TwoSpeed=1.", CoolingTowerControl.SingleSpeed);
        pManager.AddNumberParameter("Tower Capacity", "TCap", "Optional nominal cooling-tower capacity in W; leave disconnected for autosize/unset.", GH_ParamAccess.item);
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
        pManager[2].Optional = true;
        pManager[6].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonSourceSystemParam(), "Source", "S", "Authored chiller source.", GH_ParamAccess.item);
        pManager.AddParameter(new GonieGonie.InvisibleDragon.Grasshopper.Parameters.DiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Chiller";
        double cop = 3d;
        int compressor = (int)CompressorType.Turbo;
        int towerType = (int)CoolingTowerType.Open;
        int towerControl = (int)CoolingTowerControl.SingleSpeed;
        string id = string.Empty;
        if (!DA.GetData(0, ref name)
            || !DA.GetData(1, ref cop)
            || !DA.GetData(3, ref compressor)
            || !DA.GetData(4, ref towerType)
            || !DA.GetData(5, ref towerControl))
        {
            return;
        }

        double? coolingCapacity = OptionalNumber(DA, 2);
        double? towerCapacity = OptionalNumber(DA, 6);
        DA.GetData(7, ref id);
        Author(
            DA,
            1,
            "SD.GH.HVAC.CHILLER_INVALID",
            "Use the named compressor/tower values and positive COP/capacities.",
            () =>
            {
                var source = new SourceSystem(
                    name,
                    SourceSystemType.Chiller,
                    coolingCop: cop,
                    coolingCapacity: coolingCapacity,
                    compressorType: EnumValue<CompressorType>(compressor, "Compressor"),
                    coolingTowerType: EnumValue<CoolingTowerType>(towerType, "Tower Circuit"),
                    coolingTowerCapacity: towerCapacity,
                    coolingTowerControl: EnumValue<CoolingTowerControl>(towerControl, "Tower Control"),
                    id: OptionalId(id));
                DA.SetData(0, new SimpleDragonSourceSystemGoo(source));
            });
    }
}

public sealed class SimpleDragonAbsorptionChillerComponent : SimpleDragonHvacComponent
{
    public SimpleDragonAbsorptionChillerComponent()
        : base(
            "SimpleDragon Absorption Chiller",
            "SD Absorption",
            "Creates an absorption chiller with a fuel-fired generator boiler. Cooling capacity is watts; disconnected capacity remains autosized/unset.")
    {
    }

    public override Guid ComponentGuid => new("c86733d7-2074-4688-8b49-e3da13de24b7");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Absorption-chiller name.", GH_ParamAccess.item, "Absorption Chiller");
        AddFuelParameter(pManager, "Generator-boiler fuel.", FuelType.NaturalGas);
        pManager.AddNumberParameter("Thermal COP", "COP", "Dimensionless thermal cooling COP (> 0).", GH_ParamAccess.item, 0.9d);
        pManager.AddNumberParameter("Cooling Capacity", "Cap", "Optional nominal cooling capacity in W; leave disconnected for autosize/unset.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Boiler Efficiency", "Eff", "Generator-boiler thermal efficiency fraction in (0, 1].", GH_ParamAccess.item, 0.85d);
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
        pManager[3].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonSourceSystemParam(), "Source", "S", "Authored absorption-chiller source.", GH_ParamAccess.item);
        pManager.AddParameter(new GonieGonie.InvisibleDragon.Grasshopper.Parameters.DiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Absorption Chiller";
        int fuel = (int)FuelType.NaturalGas;
        double cop = 0.9d;
        double efficiency = 0.85d;
        string id = string.Empty;
        if (!DA.GetData(0, ref name)
            || !DA.GetData(1, ref fuel)
            || !DA.GetData(2, ref cop)
            || !DA.GetData(4, ref efficiency))
        {
            return;
        }

        double? capacity = OptionalNumber(DA, 3);
        DA.GetData(5, ref id);
        Author(
            DA,
            1,
            "SD.GH.HVAC.ABSORPTION_INVALID",
            "Use a listed generator fuel, positive COP/capacity, and boiler efficiency in (0, 1].",
            () =>
            {
                var source = new SourceSystem(
                    name,
                    SourceSystemType.AbsorptionChiller,
                    EnumValue<FuelType>(fuel, "Fuel"),
                    coolingCop: cop,
                    coolingCapacity: capacity,
                    boilerEfficiency: efficiency,
                    id: OptionalId(id));
                DA.SetData(0, new SimpleDragonSourceSystemGoo(source));
            });
    }
}

public sealed class SimpleDragonBoilerComponent : SimpleDragonHvacComponent
{
    public SimpleDragonBoilerComponent()
        : base(
            "SimpleDragon Boiler",
            "SD Boiler",
            "Creates a hot-water boiler source. Heating capacity is watts; disconnected capacity remains autosized/unset.")
    {
    }

    public override Guid ComponentGuid => new("7b973e2c-7254-4730-9326-c320abedde5a");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Boiler name.", GH_ParamAccess.item, "Boiler");
        AddFuelParameter(pManager, "Boiler fuel.", FuelType.NaturalGas);
        pManager.AddNumberParameter("Efficiency", "Eff", "Nominal thermal efficiency fraction in (0, 1].", GH_ParamAccess.item, 0.85d);
        pManager.AddNumberParameter("Heating Capacity", "Cap", "Optional nominal heating capacity in W; leave disconnected for autosize/unset.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Hot Water Supply", "DHW", "Whether the boiler also serves domestic hot water metadata.", GH_ParamAccess.item, false);
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
        pManager[3].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonSourceSystemParam(), "Source", "S", "Authored boiler source.", GH_ParamAccess.item);
        pManager.AddParameter(new GonieGonie.InvisibleDragon.Grasshopper.Parameters.DiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Boiler";
        int fuel = (int)FuelType.NaturalGas;
        double efficiency = 0.85d;
        bool hotWater = false;
        string id = string.Empty;
        if (!DA.GetData(0, ref name)
            || !DA.GetData(1, ref fuel)
            || !DA.GetData(2, ref efficiency)
            || !DA.GetData(4, ref hotWater))
        {
            return;
        }

        double? capacity = OptionalNumber(DA, 3);
        DA.GetData(5, ref id);
        Author(
            DA,
            1,
            "SD.GH.HVAC.BOILER_INVALID",
            "Use a listed fuel, positive capacity, and efficiency in (0, 1].",
            () =>
            {
                var source = new SourceSystem(
                    name,
                    SourceSystemType.Boiler,
                    EnumValue<FuelType>(fuel, "Fuel"),
                    heatingCapacity: capacity,
                    efficiency: efficiency,
                    hotWaterSupply: hotWater,
                    id: OptionalId(id));
                DA.SetData(0, new SimpleDragonSourceSystemGoo(source));
            });
    }
}

public sealed class SimpleDragonDistrictHeatingComponent : SimpleDragonHvacComponent
{
    public SimpleDragonDistrictHeatingComponent()
        : base(
            "SimpleDragon District Heating",
            "SD District Heat",
            "Creates an explicit district-heating source rather than a local fuel-fired boiler. Capacity is watts.")
    {
    }

    public override Guid ComponentGuid => new("8216afdf-f5c1-4f3f-ae1f-0061813af720");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "District-heating service name.", GH_ParamAccess.item, "District Heating");
        pManager.AddNumberParameter("Heating Capacity", "Cap", "Optional nominal heating capacity in W; leave disconnected for autosize/unset.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Hot Water Supply", "DHW", "Whether the service also supplies domestic hot water metadata.", GH_ParamAccess.item, false);
        pManager.AddTextParameter("ID", "ID", "Optional stable ID; leave empty for a deterministic content-derived ID.", GH_ParamAccess.item, string.Empty);
        pManager[1].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonSourceSystemParam(), "Source", "S", "Authored district-heating source.", GH_ParamAccess.item);
        pManager.AddParameter(new GonieGonie.InvisibleDragon.Grasshopper.Parameters.DiagnosticParam(), "Diagnostics", "D", "Authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "District Heating";
        bool hotWater = false;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) || !DA.GetData(2, ref hotWater))
        {
            return;
        }

        double? capacity = OptionalNumber(DA, 1);
        DA.GetData(3, ref id);
        Author(
            DA,
            1,
            "SD.GH.HVAC.DISTRICT_INVALID",
            "Use a non-empty name and a positive optional capacity.",
            () =>
            {
                var source = new SourceSystem(
                    name,
                    SourceSystemType.DistrictHeating,
                    heatingCapacity: capacity,
                    hotWaterSupply: hotWater,
                    id: OptionalId(id));
                DA.SetData(0, new SimpleDragonSourceSystemGoo(source));
            });
    }
}
