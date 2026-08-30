using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Hvac;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

public sealed class HeatPumpComponent : DragonComponent
{
    public HeatPumpComponent()
        : base(
            "Heat Pump",
            "HeatPump",
            "Creates an air-source heat pump with explicit heating and cooling performance.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("e8751fda-24b9-4727-ad66-f81de722f64f");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Source-system name.", GH_ParamAccess.item, "Heat Pump");
        ChoiceInputs.AddEnum(
            pManager,
            "Fuel",
            "F",
            "Fuel selection. Heat pumps normally use Electricity.",
            Fuel.Electricity);
        pManager.AddNumberParameter("Heating COP", "HCOP", "Rated heating coefficient of performance.", GH_ParamAccess.item, 3.5);
        pManager.AddNumberParameter("Cooling COP", "CCOP", "Rated cooling coefficient of performance.", GH_ParamAccess.item, 4.0);
        pManager.AddNumberParameter("Heating Capacity", "HCap", "Rated heating capacity in W; 0 means autosize.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Cooling Capacity", "CCap", "Rated cooling capacity in W; 0 means autosize.", GH_ParamAccess.item, 0);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSourceSystemParam(), "Source", "S", "InvisibleDragon heat-pump source.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Heat Pump";
        string fuelText = nameof(Fuel.Electricity);
        double heatingCop = 3.5;
        double coolingCop = 4.0;
        double heatingCapacity = 0;
        double coolingCapacity = 0;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref fuelText) ||
            !DA.GetData(2, ref heatingCop) ||
            !DA.GetData(3, ref coolingCop) ||
            !DA.GetData(4, ref heatingCapacity) ||
            !DA.GetData(5, ref coolingCapacity))
        {
            return;
        }

        Fuel fuel = ChoiceInputs.ParseEnum<Fuel>(fuelText, "Fuel");
        var source = new HeatPump(
            StableIds.Create(
                "heat-pump",
                name,
                fuel.ToString(),
                HvacComponentSupport.Number(heatingCop),
                HvacComponentSupport.Number(coolingCop),
                HvacComponentSupport.Number(heatingCapacity),
                HvacComponentSupport.Number(coolingCapacity)),
            name,
            fuel,
            heatingCop,
            coolingCop,
            HvacComponentSupport.OptionalPositive(heatingCapacity, "Heating Capacity"),
            HvacComponentSupport.OptionalPositive(coolingCapacity, "Cooling Capacity"));
        DA.SetData(0, new DragonSourceSystemGoo(source));
    }
}

public sealed class GeothermalHeatPumpComponent : DragonComponent
{
    public GeothermalHeatPumpComponent()
        : base(
            "Geothermal Heat Pump",
            "GeoHeatPump",
            "Creates a geothermal heat-pump identity using the shared heat-pump performance model.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("ccfa3a94-c7ea-4011-8b0f-b3364f4c023a");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Source-system name.", GH_ParamAccess.item, "Geothermal Heat Pump");
        ChoiceInputs.AddEnum(
            pManager,
            "Fuel",
            "F",
            "Fuel selection. Geothermal heat pumps normally use Electricity.",
            Fuel.Electricity);
        pManager.AddNumberParameter("Heating COP", "HCOP", "Rated heating coefficient of performance.", GH_ParamAccess.item, 4.0);
        pManager.AddNumberParameter("Cooling COP", "CCOP", "Rated cooling coefficient of performance.", GH_ParamAccess.item, 5.0);
        pManager.AddNumberParameter("Heating Capacity", "HCap", "Rated heating capacity in W; 0 means autosize.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Cooling Capacity", "CCap", "Rated cooling capacity in W; 0 means autosize.", GH_ParamAccess.item, 0);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSourceSystemParam(), "Source", "S", "InvisibleDragon geothermal heat-pump source.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Geothermal Heat Pump";
        string fuelText = nameof(Fuel.Electricity);
        double heatingCop = 4.0;
        double coolingCop = 5.0;
        double heatingCapacity = 0;
        double coolingCapacity = 0;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref fuelText) ||
            !DA.GetData(2, ref heatingCop) ||
            !DA.GetData(3, ref coolingCop) ||
            !DA.GetData(4, ref heatingCapacity) ||
            !DA.GetData(5, ref coolingCapacity))
        {
            return;
        }

        Fuel fuel = ChoiceInputs.ParseEnum<Fuel>(fuelText, "Fuel");
        var source = new GeothermalHeatPump(
            StableIds.Create(
                "geothermal-heat-pump",
                name,
                fuel.ToString(),
                HvacComponentSupport.Number(heatingCop),
                HvacComponentSupport.Number(coolingCop),
                HvacComponentSupport.Number(heatingCapacity),
                HvacComponentSupport.Number(coolingCapacity)),
            name,
            fuel,
            heatingCop,
            coolingCop,
            HvacComponentSupport.OptionalPositive(heatingCapacity, "Heating Capacity"),
            HvacComponentSupport.OptionalPositive(coolingCapacity, "Cooling Capacity"));
        DA.SetData(0, new DragonSourceSystemGoo(source));
    }
}

public sealed class CoolingTowerComponent : DragonComponent
{
    public CoolingTowerComponent()
        : base(
            "Cooling Tower",
            "Tower",
            "Creates an open cooling tower or closed fluid cooler with one or two fan speeds.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("68084dee-fa5c-4669-b3c0-d64e9aca182b");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Cooling-tower name.", GH_ParamAccess.item, "Cooling Tower");
        ChoiceInputs.Add(
            pManager,
            "Circuit",
            "C",
            "Circuit selection: Open cooling tower or Closed fluid cooler.",
            "Open",
            "Open",
            "Closed");
        ChoiceInputs.Add(
            pManager,
            "Fan Speeds",
            "S",
            "Fan-speed selection: Single or Two speed.",
            "Single",
            "Single",
            "Two");
        pManager.AddNumberParameter("Nominal Capacity", "Cap", "Heat-rejection capacity in W; 0 means autosize.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Pump Motor Efficiency", "Eff", "Condenser-loop pump motor efficiency from 0 to 1.", GH_ParamAccess.item, 0.9);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddGenericParameter("Cooling Tower", "T", "CoolingTower domain value for Chiller components.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Cooling Tower";
        string circuitText = "Open";
        string speedText = "Single";
        double nominalCapacity = 0;
        double pumpEfficiency = 0.9;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref circuitText) ||
            !DA.GetData(2, ref speedText) ||
            !DA.GetData(3, ref nominalCapacity) ||
            !DA.GetData(4, ref pumpEfficiency))
        {
            return;
        }

        string circuit = ChoiceInputs.Parse(circuitText, "Circuit", "Open", "Closed");
        string speed = ChoiceInputs.Parse(speedText, "Fan Speeds", "Single", "Two");

        var towerId = StableIds.Create(
            "cooling-tower",
            name,
            circuit,
            speed,
            HvacComponentSupport.Number(nominalCapacity),
            HvacComponentSupport.Number(pumpEfficiency));
        double? capacity = HvacComponentSupport.OptionalPositive(nominalCapacity, "Nominal Capacity");
        CoolingTower tower = (circuit, speed) switch
        {
            ("Open", "Single") => new OpenSingleSpeedCoolingTower(towerId, name, capacity, pumpEfficiency),
            ("Open", "Two") => new OpenTwoSpeedCoolingTower(towerId, name, capacity, pumpEfficiency),
            ("Closed", "Single") => new ClosedSingleSpeedCoolingTower(towerId, name, capacity, pumpEfficiency),
            _ => new ClosedTwoSpeedCoolingTower(towerId, name, capacity, pumpEfficiency),
        };
        DA.SetData(0, new GH_ObjectWrapper(tower));
    }
}

public sealed class ChillerComponent : DragonComponent
{
    public ChillerComponent()
        : base(
            "Chiller",
            "Chiller",
            "Creates an electric vapor-compression chiller connected to a cooling tower.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("a4254427-84f7-4ba3-9c8a-2aea8862fde6");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Source-system name.", GH_ParamAccess.item, "Chiller");
        pManager.AddNumberParameter("Reference COP", "COP", "Reference electric coefficient of performance.", GH_ParamAccess.item, 5.0);
        ChoiceInputs.AddEnum(
            pManager,
            "Compressor",
            "Comp",
            "Compressor selection: Turbo, Screw, or Reciprocating.",
            CompressorType.Turbo);

        pManager.AddGenericParameter("Cooling Tower", "T", "CoolingTower value created by the Cooling Tower component.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Nominal Capacity", "Cap", "Rated cooling capacity in W; 0 means autosize.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Pump Motor Efficiency", "Eff", "Chilled-water pump motor efficiency from 0 to 1.", GH_ParamAccess.item, 0.9);
        pManager.AddNumberParameter("Chilled Water Setpoint", "Tset", "Chilled-water supply setpoint in degrees C.", GH_ParamAccess.item, 6.0);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSourceSystemParam(), "Source", "S", "InvisibleDragon chiller source.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Chiller";
        double referenceCop = 5.0;
        string compressorText = nameof(CompressorType.Turbo);
        object? towerObject = null;
        double nominalCapacity = 0;
        double pumpEfficiency = 0.9;
        double setpoint = 6.0;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref referenceCop) ||
            !DA.GetData(2, ref compressorText) ||
            !DA.GetData(3, ref towerObject) ||
            !DA.GetData(4, ref nominalCapacity) ||
            !DA.GetData(5, ref pumpEfficiency) ||
            !DA.GetData(6, ref setpoint))
        {
            return;
        }

        CoolingTower tower = HvacComponentSupport.RequireObject<CoolingTower>(towerObject, "Cooling Tower");
        CompressorType compressor = ChoiceInputs.ParseEnum<CompressorType>(compressorText, "Compressor");
        var source = new Chiller(
            StableIds.Create(
                "chiller",
                name,
                compressor.ToString(),
                tower.Id.Value,
                HvacComponentSupport.Number(referenceCop),
                HvacComponentSupport.Number(nominalCapacity),
                HvacComponentSupport.Number(pumpEfficiency),
                HvacComponentSupport.Number(setpoint)),
            name,
            referenceCop,
            compressor,
            tower,
            HvacComponentSupport.OptionalPositive(nominalCapacity, "Nominal Capacity"),
            pumpEfficiency,
            setpoint);
        DA.SetData(0, new DragonSourceSystemGoo(source));
    }
}

public sealed class AbsorptionChillerComponent : DragonComponent
{
    public AbsorptionChillerComponent()
        : base(
            "Absorption Chiller",
            "AbsChiller",
            "Creates a thermally driven absorption chiller with a boiler generator and cooling tower.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("5719d04d-3093-4293-87d9-17f5bd9d9a7e");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Source-system name.", GH_ParamAccess.item, "Absorption Chiller");
        pManager.AddNumberParameter("Thermal COP", "COP", "Rated thermal coefficient of performance.", GH_ParamAccess.item, 1.0);
        pManager.AddParameter(new DragonSourceSystemParam(), "Generator Boiler", "B", "Boiler source supplying generator heat.", GH_ParamAccess.item);
        pManager.AddGenericParameter("Cooling Tower", "T", "CoolingTower value created by the Cooling Tower component.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Nominal Capacity", "Cap", "Rated cooling capacity in W; 0 means autosize.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Pump Motor Efficiency", "Eff", "Chilled-water pump motor efficiency from 0 to 1.", GH_ParamAccess.item, 0.9);
        pManager.AddNumberParameter("Chilled Water Setpoint", "Tset", "Chilled-water supply setpoint in degrees C.", GH_ParamAccess.item, 6.0);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSourceSystemParam(), "Source", "S", "InvisibleDragon absorption-chiller source.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Absorption Chiller";
        double thermalCop = 1.0;
        DragonSourceSystemGoo? boilerGoo = null;
        object? towerObject = null;
        double nominalCapacity = 0;
        double pumpEfficiency = 0.9;
        double setpoint = 6.0;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref thermalCop) ||
            !DA.GetData(2, ref boilerGoo) ||
            !DA.GetData(3, ref towerObject) ||
            !DA.GetData(4, ref nominalCapacity) ||
            !DA.GetData(5, ref pumpEfficiency) ||
            !DA.GetData(6, ref setpoint))
        {
            return;
        }

        Boiler boiler = HvacComponentSupport.Source<Boiler>(boilerGoo, "Generator Boiler");
        CoolingTower tower = HvacComponentSupport.RequireObject<CoolingTower>(towerObject, "Cooling Tower");
        var source = new AbsorptionChiller(
            StableIds.Create(
                "absorption-chiller",
                name,
                boiler.Id.Value,
                tower.Id.Value,
                HvacComponentSupport.Number(thermalCop),
                HvacComponentSupport.Number(nominalCapacity),
                HvacComponentSupport.Number(pumpEfficiency),
                HvacComponentSupport.Number(setpoint)),
            name,
            thermalCop,
            boiler,
            tower,
            HvacComponentSupport.OptionalPositive(nominalCapacity, "Nominal Capacity"),
            pumpEfficiency,
            setpoint);
        DA.SetData(0, new DragonSourceSystemGoo(source));
    }
}

public sealed class BoilerComponent : DragonComponent
{
    public BoilerComponent()
        : base(
            "Boiler",
            "Boiler",
            "Creates a hot-water boiler source with explicit fuel and plant-loop performance.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("e732f5f9-db94-405b-9221-f4449b4baad7");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Source-system name.", GH_ParamAccess.item, "Boiler");
        ChoiceInputs.AddEnum(pManager, "Fuel", "F", "Boiler fuel selection.", Fuel.NaturalGas);
        pManager.AddNumberParameter("Thermal Efficiency", "Eff", "Nominal thermal efficiency from 0 to 1.", GH_ParamAccess.item, 0.9);
        pManager.AddNumberParameter("Nominal Capacity", "Cap", "Rated heating capacity in W; 0 means autosize.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Pump Motor Efficiency", "Pump", "Hot-water pump motor efficiency from 0 to 1.", GH_ParamAccess.item, 0.9);
        pManager.AddNumberParameter("Hot Water Setpoint", "Tset", "Hot-water supply setpoint in degrees C.", GH_ParamAccess.item, 60.0);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSourceSystemParam(), "Source", "S", "InvisibleDragon boiler source.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Boiler";
        string fuelText = nameof(Fuel.NaturalGas);
        double efficiency = 0.9;
        double nominalCapacity = 0;
        double pumpEfficiency = 0.9;
        double setpoint = 60;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref fuelText) ||
            !DA.GetData(2, ref efficiency) ||
            !DA.GetData(3, ref nominalCapacity) ||
            !DA.GetData(4, ref pumpEfficiency) ||
            !DA.GetData(5, ref setpoint))
        {
            return;
        }

        Fuel fuel = ChoiceInputs.ParseEnum<Fuel>(fuelText, "Fuel");
        var source = new Boiler(
            StableIds.Create(
                "boiler",
                name,
                fuel.ToString(),
                HvacComponentSupport.Number(efficiency),
                HvacComponentSupport.Number(nominalCapacity),
                HvacComponentSupport.Number(pumpEfficiency),
                HvacComponentSupport.Number(setpoint)),
            name,
            fuel,
            efficiency,
            HvacComponentSupport.OptionalPositive(nominalCapacity, "Nominal Capacity"),
            pumpEfficiency,
            setpoint);
        DA.SetData(0, new DragonSourceSystemGoo(source));
    }
}

public sealed class DistrictHeatingComponent : DragonComponent
{
    public DistrictHeatingComponent()
        : base(
            "District Heating",
            "DistrictHeat",
            "Creates a district hot-water source and distribution loop.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("e768769e-3a89-425d-9f99-3610e8e43bb9");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Source-system name.", GH_ParamAccess.item, "District Heating");
        pManager.AddNumberParameter("Nominal Capacity", "Cap", "Available heating capacity in W; 0 means autosize.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Pump Motor Efficiency", "Pump", "Distribution-pump motor efficiency from 0 to 1.", GH_ParamAccess.item, 0.9);
        pManager.AddNumberParameter("Hot Water Setpoint", "Tset", "Hot-water supply setpoint in degrees C.", GH_ParamAccess.item, 60.0);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSourceSystemParam(), "Source", "S", "InvisibleDragon district-heating source.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "District Heating";
        double nominalCapacity = 0;
        double pumpEfficiency = 0.9;
        double setpoint = 60;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref nominalCapacity) ||
            !DA.GetData(2, ref pumpEfficiency) ||
            !DA.GetData(3, ref setpoint))
        {
            return;
        }

        var source = new DistrictHeating(
            StableIds.Create(
                "district-heating",
                name,
                HvacComponentSupport.Number(nominalCapacity),
                HvacComponentSupport.Number(pumpEfficiency),
                HvacComponentSupport.Number(setpoint)),
            name,
            HvacComponentSupport.OptionalPositive(nominalCapacity, "Nominal Capacity"),
            pumpEfficiency,
            setpoint);
        DA.SetData(0, new DragonSourceSystemGoo(source));
    }
}
