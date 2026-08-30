using Grasshopper.Kernel;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Hvac;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

public sealed class DomesticHotWaterComponent : DragonComponent
{
    public DomesticHotWaterComponent()
        : base(
            "Domestic Hot Water",
            "DHW",
            "Creates a domestic-hot-water system with an explicit fuel and conversion efficiency.",
            "HVAC")
    {
    }

    public override Guid ComponentGuid => new("6f59e771-5dc0-44aa-9b7d-a84c3d0c7d74");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Domestic-hot-water system name.", GH_ParamAccess.item, "Domestic Hot Water");
        ChoiceInputs.AddEnum(
            pManager,
            "Fuel",
            "F",
            "Fuel selection.",
            Fuel.NaturalGas);
        pManager.AddNumberParameter(
            "Efficiency",
            "Eff",
            "Fuel-to-water conversion efficiency greater than 0 and no greater than 1.",
            GH_ParamAccess.item,
            0.85);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonDomesticHotWaterParam(),
            "Domestic Hot Water",
            "DHW",
            "InvisibleDragon domestic-hot-water system.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Domestic Hot Water";
        string fuelText = nameof(Fuel.NaturalGas);
        double efficiency = 0.85;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref fuelText) ||
            !DA.GetData(2, ref efficiency))
        {
            return;
        }

        Fuel fuel = ChoiceInputs.ParseEnum<Fuel>(fuelText, "Fuel");
        var domesticHotWater = new DomesticHotWater(
            StableIds.Create(
                "domestic-hot-water",
                name,
                fuel.ToString(),
                HvacComponentSupport.Number(efficiency)),
            name,
            fuel,
            efficiency);
        DA.SetData(0, new DragonDomesticHotWaterGoo(domesticHotWater));
    }
}

public sealed class EnergyRecoveryVentilatorComponent : DragonComponent
{
    public EnergyRecoveryVentilatorComponent()
        : base(
            "Energy Recovery Ventilator",
            "ERV",
            "Creates a sensible-and-latent energy recovery ventilator owned directly by a Zone.",
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
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref sensible) ||
            !DA.GetData(2, ref latent) ||
            !DA.GetData(3, ref flow) ||
            !DA.GetData(4, ref fanEfficiency) ||
            !DA.GetData(5, ref pressureRise))
        {
            return;
        }

        var ventilator = new EnergyRecoveryVentilator(
            StableIds.Create("energy-recovery-ventilator", name),
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
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref area) ||
            !DA.GetData(2, ref tilt) ||
            !DA.GetData(3, ref azimuth) ||
            !DA.GetData(4, ref efficiency) ||
            !DA.GetData(5, ref activeCellFraction))
        {
            return;
        }

        var panel = new PhotovoltaicPanel(
            StableIds.Create("photovoltaic-panel", name),
            name,
            area,
            tilt,
            azimuth,
            efficiency,
            activeCellFraction);
        DA.SetData(0, new DragonPhotovoltaicPanelGoo(panel));
    }
}
