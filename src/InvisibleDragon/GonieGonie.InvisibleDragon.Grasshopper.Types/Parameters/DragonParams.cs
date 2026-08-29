using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace GonieGonie.InvisibleDragon.Grasshopper.Parameters;

public abstract class DragonParam<TGoo> : GH_PersistentParam<TGoo>
    where TGoo : class, IGH_Goo, new()
{
    protected DragonParam(string name, string nickname, string description)
        : base(name, nickname, description, "InvisibleDragon", "Parameters")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;

    protected override Bitmap? Icon => ParameterIcons.ForParameter(GetType());

    protected override GH_GetterResult Prompt_Singular(ref TGoo value)
    {
        return GH_GetterResult.cancel;
    }

    protected override GH_GetterResult Prompt_Plural(ref List<TGoo> values)
    {
        return GH_GetterResult.cancel;
    }
}

public sealed class DragonMaterialParam : DragonParam<Types.DragonMaterialGoo>
{
    public DragonMaterialParam() : base("InvisibleDragon Material", "Material", "An InvisibleDragon opaque material.") { }
    public override Guid ComponentGuid => new("02652d26-0b4e-467f-b079-c660bb7243c2");
}

public sealed class DragonConstructionParam : DragonParam<Types.DragonConstructionGoo>
{
    public DragonConstructionParam() : base("InvisibleDragon Construction", "Construction", "An InvisibleDragon surface construction.") { }
    public override Guid ComponentGuid => new("3e7d571e-6914-47b1-b130-7bd1b2121a86");
}

public sealed class DragonScheduleParam : DragonParam<Types.DragonScheduleGoo>
{
    public DragonScheduleParam() : base("InvisibleDragon Schedule", "Schedule", "An InvisibleDragon annual schedule.") { }
    public override Guid ComponentGuid => new("8aa326cf-4bcb-4386-aa90-4b81a851355c");
}

public sealed class DragonProfileParam : DragonParam<Types.DragonProfileGoo>
{
    public DragonProfileParam() : base("InvisibleDragon Profile", "Profile", "An InvisibleDragon zone usage profile.") { }
    public override Guid ComponentGuid => new("39d3b7f4-4287-41a5-b260-d61077b88b55");
}

public sealed class DragonSurfaceParam : DragonParam<Types.DragonSurfaceGoo>
{
    public DragonSurfaceParam() : base("InvisibleDragon Surface", "Surface", "An InvisibleDragon polygon surface.") { }
    public override Guid ComponentGuid => new("1ce3f493-c9c4-4549-893a-0a950998da62");
}

public sealed class DragonZoneParam : DragonParam<Types.DragonZoneGoo>
{
    public DragonZoneParam() : base("InvisibleDragon Zone", "Zone", "An InvisibleDragon thermal zone.") { }
    public override Guid ComponentGuid => new("cff53fa0-0cc2-4c50-832e-fdf82691b9cc");
}

public sealed class DragonEnergyModelParam : DragonParam<Types.DragonEnergyModelGoo>
{
    public DragonEnergyModelParam() : base("InvisibleDragon Energy Model", "Model", "An InvisibleDragon EnergyPlus model.") { }
    public override Guid ComponentGuid => new("dbfba1b5-624a-4db4-8fec-d80eb9561467");
}

public sealed class DragonSourceSystemParam : DragonParam<Types.DragonSourceSystemGoo>
{
    public DragonSourceSystemParam() : base("InvisibleDragon Source System", "Source", "An InvisibleDragon HVAC source system.") { }
    public override Guid ComponentGuid => new("d7597f76-1486-45b7-bcc6-7e8f5fb23738");
}

public sealed class DragonSupplySystemParam : DragonParam<Types.DragonSupplySystemGoo>
{
    public DragonSupplySystemParam() : base("InvisibleDragon Supply System", "Supply", "An InvisibleDragon zone HVAC supply system.") { }
    public override Guid ComponentGuid => new("c6afcc1f-f11e-4a54-a84a-0e845a828d5d");
}

public sealed class DragonDomesticHotWaterParam : DragonParam<Types.DragonDomesticHotWaterGoo>
{
    public DragonDomesticHotWaterParam()
        : base("InvisibleDragon Domestic Hot Water", "DHW", "An InvisibleDragon domestic-hot-water system.") { }
    public override Guid ComponentGuid => new("9a8b4d80-3088-4898-9ea9-2743312aa1ae");
}

public sealed class DragonEnergyRecoveryVentilatorParam : DragonParam<Types.DragonEnergyRecoveryVentilatorGoo>
{
    public DragonEnergyRecoveryVentilatorParam()
        : base("InvisibleDragon Energy Recovery Ventilator", "ERV", "An InvisibleDragon energy-recovery ventilator.") { }
    public override Guid ComponentGuid => new("bc8c67a8-e853-4eec-a576-acdeedbe371b");
}

public sealed class DragonPhotovoltaicPanelParam : DragonParam<Types.DragonPhotovoltaicPanelGoo>
{
    public DragonPhotovoltaicPanelParam()
        : base("InvisibleDragon Photovoltaic Panel", "PV", "An InvisibleDragon photovoltaic panel.") { }
    public override Guid ComponentGuid => new("26ef6130-77e3-4c6d-a802-9460bcc386ed");
}

public sealed class DragonIdfParam : DragonParam<Types.DragonIdfGoo>
{
    public DragonIdfParam() : base("InvisibleDragon IDF", "IDF", "An assembled EnergyPlus IDF document.") { }
    public override Guid ComponentGuid => new("fc64602d-d9bc-4052-a563-c7f8ea77ae99");
}

public sealed class EnergyPlusResultParam : DragonParam<Types.EnergyPlusResultGoo>
{
    public EnergyPlusResultParam() : base("EnergyPlus Result", "Result", "A structured EnergyPlus simulation result.") { }
    public override Guid ComponentGuid => new("3aded2aa-eaa9-4154-a7bc-736dd8bc783f");
}

public sealed class DiagnosticParam : DragonParam<Types.DiagnosticGoo>
{
    public DiagnosticParam() : base("InvisibleDragon Diagnostic", "Diagnostic", "A validation or execution diagnostic.") { }
    public override Guid ComponentGuid => new("84cffc02-1023-428b-b96a-e327b5a73c65");
}

public sealed class PreparedWeatherFileParam : DragonParam<Types.PreparedWeatherFileGoo>
{
    public PreparedWeatherFileParam()
        : base(
            "InvisibleDragon Prepared Weather",
            "Weather",
            "A content-addressed EPW artifact prepared for InvisibleDragon execution.")
    {
    }

    public override Guid ComponentGuid => new("9571341c-3795-417d-9908-5833d234d815");
}
