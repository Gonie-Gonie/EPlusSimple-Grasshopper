using Grasshopper.Kernel;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Hvac;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

public sealed class PackagedAirConditionerComponent : DragonComponent
{
    public PackagedAirConditionerComponent()
        : base(
            "Packaged Air Conditioner",
            "PackagedAC",
            "Creates a cooling-only packaged terminal connected to a heat-pump source.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("c78b3a6c-5517-4c56-ad1d-b0da8bfc37c3");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Supply-system name.", GH_ParamAccess.item, "Packaged AC");
        pManager.AddParameter(new DragonSourceSystemParam(), "Heat Pump", "HP", "HeatPump or GeothermalHeatPump source.", GH_ParamAccess.item);
        pManager.AddTextParameter("ID", "ID", "Optional stable supply-system identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSupplySystemParam(), "Supply", "S", "InvisibleDragon packaged air conditioner.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Packaged AC";
        DragonSourceSystemGoo? sourceGoo = null;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) || !DA.GetData(1, ref sourceGoo))
        {
            return;
        }

        DA.GetData(2, ref id);
        HeatPump source = HvacComponentSupport.Source<HeatPump>(sourceGoo, "Heat Pump");
        var supply = new PackagedAirConditioner(
            StableIds.Resolve(id, "packaged-air-conditioner", name, source.Id.Value),
            name,
            source);
        DA.SetData(0, new DragonSupplySystemGoo(supply));
    }
}

public sealed class AirHandlingUnitComponent : DragonComponent
{
    public AirHandlingUnitComponent()
        : base(
            "Air Handling Unit",
            "AHU",
            "Creates a heating-and-cooling air terminal connected to a heat-pump source.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("a3a4afd8-17e1-4d9f-8da5-5883331c360f");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Supply-system name.", GH_ParamAccess.item, "Air Handling Unit");
        pManager.AddParameter(new DragonSourceSystemParam(), "Heat Pump", "HP", "HeatPump or GeothermalHeatPump source.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Fan Total Efficiency", "FanEff", "Supply-fan total efficiency from 0 to 1.", GH_ParamAccess.item, 0.7);
        pManager.AddNumberParameter("Fan Pressure Rise", "dP", "Supply-fan pressure rise in Pa.", GH_ParamAccess.item, 100.0);
        pManager.AddNumberParameter("Motor Efficiency", "Motor", "Fan motor efficiency from 0 to 1.", GH_ParamAccess.item, 0.9);
        pManager.AddTextParameter("ID", "ID", "Optional stable supply-system identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSupplySystemParam(), "Supply", "S", "InvisibleDragon air handling unit.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Air Handling Unit";
        DragonSourceSystemGoo? sourceGoo = null;
        double fanEfficiency = 0.7;
        double pressureRise = 100;
        double motorEfficiency = 0.9;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref sourceGoo) ||
            !DA.GetData(2, ref fanEfficiency) ||
            !DA.GetData(3, ref pressureRise) ||
            !DA.GetData(4, ref motorEfficiency))
        {
            return;
        }

        DA.GetData(5, ref id);
        HeatPump source = HvacComponentSupport.Source<HeatPump>(sourceGoo, "Heat Pump");
        var supply = new AirHandlingUnit(
            StableIds.Resolve(id, "air-handling-unit", name, source.Id.Value),
            name,
            source,
            fanEfficiency,
            pressureRise,
            motorEfficiency);
        DA.SetData(0, new DragonSupplySystemGoo(supply));
    }
}

public sealed class FanCoilUnitComponent : DragonComponent
{
    public FanCoilUnitComponent()
        : base(
            "Fan Coil Unit",
            "FCU",
            "Creates a four-pipe fan-coil terminal on one compatible heating or cooling plant source.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("b24068e1-bd66-4d79-a1c6-aa6a79f50edc");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Supply-system name.", GH_ParamAccess.item, "Fan Coil Unit");
        pManager.AddParameter(
            new DragonSourceSystemParam(),
            "Plant Source",
            "Plant",
            "Boiler, DistrictHeating, Chiller, or AbsorptionChiller source.",
            GH_ParamAccess.item);
        pManager.AddNumberParameter("Fan Total Efficiency", "FanEff", "Fan total efficiency from 0 to 1.", GH_ParamAccess.item, 0.7);
        pManager.AddNumberParameter("Fan Pressure Rise", "dP", "Fan pressure rise in Pa.", GH_ParamAccess.item, 100.0);
        pManager.AddNumberParameter("Motor Efficiency", "Motor", "Fan motor efficiency from 0 to 1.", GH_ParamAccess.item, 0.9);
        pManager.AddTextParameter("ID", "ID", "Optional stable supply-system identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSupplySystemParam(), "Supply", "S", "InvisibleDragon fan-coil unit.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Fan Coil Unit";
        DragonSourceSystemGoo? sourceGoo = null;
        double fanEfficiency = 0.7;
        double pressureRise = 100;
        double motorEfficiency = 0.9;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref sourceGoo) ||
            !DA.GetData(2, ref fanEfficiency) ||
            !DA.GetData(3, ref pressureRise) ||
            !DA.GetData(4, ref motorEfficiency))
        {
            return;
        }

        DA.GetData(5, ref id);
        SourceSystem source = HvacComponentSupport.Source(sourceGoo, "Plant Source");
        if (source is not Boiler and not DistrictHeating and not Chiller and not AbsorptionChiller)
        {
            throw new ArgumentException(
                $"Plant Source requires Boiler, DistrictHeating, Chiller, or AbsorptionChiller; received {source.GetType().Name}.");
        }

        var supply = new FanCoilUnit(
            StableIds.Resolve(id, "fan-coil-unit", name, source.Id.Value),
            name,
            source,
            fanEfficiency,
            pressureRise,
            motorEfficiency);
        DA.SetData(0, new DragonSupplySystemGoo(supply));
    }
}

public sealed class RadiatorComponent : DragonComponent
{
    public RadiatorComponent()
        : base(
            "Radiator",
            "Radiator",
            "Creates a hydronic radiant-convective radiator on a heating plant.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("1aed82ba-f96f-453b-b2b0-7d30498659cb");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Supply-system name.", GH_ParamAccess.item, "Radiator");
        pManager.AddParameter(new DragonSourceSystemParam(), "Heating Source", "Plant", "Boiler or DistrictHeating source.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Heating Capacity", "Cap", "Rated heating capacity in W; 0 means autosize.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Radiant Fraction", "Rad", "Fraction of heat emitted radiantly, from 0 to 1.", GH_ParamAccess.item, 0);
        pManager.AddTextParameter("ID", "ID", "Optional stable supply-system identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSupplySystemParam(), "Supply", "S", "InvisibleDragon hydronic radiator.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Radiator";
        DragonSourceSystemGoo? sourceGoo = null;
        double heatingCapacity = 0;
        double radiantFraction = 0;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref sourceGoo) ||
            !DA.GetData(2, ref heatingCapacity) ||
            !DA.GetData(3, ref radiantFraction))
        {
            return;
        }

        DA.GetData(4, ref id);
        SourceSystem source = HvacComponentSupport.Source(sourceGoo, "Heating Source");
        if (source is not Boiler and not DistrictHeating)
        {
            throw new ArgumentException(
                $"Heating Source requires Boiler or DistrictHeating; received {source.GetType().Name}.");
        }

        var supply = new Radiator(
            StableIds.Resolve(id, "radiator", name, source.Id.Value),
            name,
            source,
            HvacComponentSupport.OptionalPositive(heatingCapacity, "Heating Capacity"),
            radiantFraction);
        DA.SetData(0, new DragonSupplySystemGoo(supply));
    }
}

public sealed class ElectricRadiatorComponent : DragonComponent
{
    public ElectricRadiatorComponent()
        : base(
            "Electric Radiator",
            "ElecRadiator",
            "Creates a source-free electric radiant-convective radiator.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("f18b4488-39e9-406c-b632-5e635c9972bb");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Supply-system name.", GH_ParamAccess.item, "Electric Radiator");
        pManager.AddNumberParameter("Heating Capacity", "Cap", "Rated heating capacity in W; 0 means autosize.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Efficiency", "Eff", "Electric conversion efficiency from 0 to 1.", GH_ParamAccess.item, 1.0);
        pManager.AddNumberParameter("Radiant Fraction", "Rad", "Fraction of heat emitted radiantly, from 0 to 1.", GH_ParamAccess.item, 0);
        pManager.AddTextParameter("ID", "ID", "Optional stable supply-system identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSupplySystemParam(), "Supply", "S", "InvisibleDragon electric radiator.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Electric Radiator";
        double heatingCapacity = 0;
        double efficiency = 1;
        double radiantFraction = 0;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref heatingCapacity) ||
            !DA.GetData(2, ref efficiency) ||
            !DA.GetData(3, ref radiantFraction))
        {
            return;
        }

        DA.GetData(4, ref id);
        var supply = new ElectricRadiator(
            StableIds.Resolve(id, "electric-radiator", name),
            name,
            HvacComponentSupport.OptionalPositive(heatingCapacity, "Heating Capacity"),
            efficiency,
            radiantFraction);
        DA.SetData(0, new DragonSupplySystemGoo(supply));
    }
}

public sealed class RadiantFloorComponent : DragonComponent
{
    public RadiantFloorComponent()
        : base(
            "Radiant Floor",
            "RadiantFloor",
            "Creates a hydronic low-temperature radiant-floor terminal.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("e3bd88b6-54b6-43ec-9c94-ee0e36218618");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Supply-system name.", GH_ParamAccess.item, "Radiant Floor");
        pManager.AddParameter(new DragonSourceSystemParam(), "Hydronic Source", "Plant", "Non-heat-pump hydronic plant source.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Throttling Range", "dT", "Heating control throttling range in degrees C.", GH_ParamAccess.item, 2.0);
        pManager.AddTextParameter("ID", "ID", "Optional stable supply-system identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSupplySystemParam(), "Supply", "S", "InvisibleDragon hydronic radiant floor.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Radiant Floor";
        DragonSourceSystemGoo? sourceGoo = null;
        double throttlingRange = 2;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref sourceGoo) ||
            !DA.GetData(2, ref throttlingRange))
        {
            return;
        }

        DA.GetData(3, ref id);
        SourceSystem source = HvacComponentSupport.Source(sourceGoo, "Hydronic Source");
        var supply = new RadiantFloor(
            StableIds.Resolve(id, "radiant-floor", name, source.Id.Value),
            name,
            source,
            throttlingRange);
        DA.SetData(0, new DragonSupplySystemGoo(supply));
    }
}

public sealed class ElectricRadiantFloorComponent : DragonComponent
{
    public ElectricRadiantFloorComponent()
        : base(
            "Electric Radiant Floor",
            "ElecRadiantFloor",
            "Creates a source-free electric low-temperature radiant floor.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("b59c6585-0c85-4c68-bb43-1f37e4aade22");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Supply-system name.", GH_ParamAccess.item, "Electric Radiant Floor");
        pManager.AddNumberParameter("Throttling Range", "dT", "Heating control throttling range in degrees C.", GH_ParamAccess.item, 2.0);
        pManager.AddTextParameter("ID", "ID", "Optional stable supply-system identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSupplySystemParam(), "Supply", "S", "InvisibleDragon electric radiant floor.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Electric Radiant Floor";
        double throttlingRange = 2;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) || !DA.GetData(1, ref throttlingRange))
        {
            return;
        }

        DA.GetData(2, ref id);
        var supply = new ElectricRadiantFloor(
            StableIds.Resolve(id, "electric-radiant-floor", name),
            name,
            throttlingRange);
        DA.SetData(0, new DragonSupplySystemGoo(supply));
    }
}
