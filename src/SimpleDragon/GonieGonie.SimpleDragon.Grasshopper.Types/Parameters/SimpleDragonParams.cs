using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace GonieGonie.SimpleDragon.Grasshopper.Parameters;

public abstract class SimpleDragonParam<TGoo> : GH_PersistentParam<TGoo>
    where TGoo : class, IGH_Goo, new()
{
    protected SimpleDragonParam(string name, string nickname, string description)
        : base(name, nickname, description, "SimpleDragon", "Parameters")
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

public sealed class SimpleDragonMaterialParam : SimpleDragonParam<Types.SimpleDragonMaterialGoo>
{
    public SimpleDragonMaterialParam() : base("SimpleDragon Material", "Material", "A SimpleDragon opaque material.") { }
    public override Guid ComponentGuid => new("f03481bf-f063-4991-b610-82af1d11aeae");
}

public sealed class SimpleDragonSurfaceConstructionParam : SimpleDragonParam<Types.SimpleDragonSurfaceConstructionGoo>
{
    public SimpleDragonSurfaceConstructionParam() : base("SimpleDragon Surface Construction", "Construction", "A layered SimpleDragon opaque construction.") { }
    public override Guid ComponentGuid => new("47c8d82e-58f7-48e5-859e-781418eaf42f");
}

public sealed class SimpleDragonFenestrationConstructionParam : SimpleDragonParam<Types.SimpleDragonFenestrationConstructionGoo>
{
    public SimpleDragonFenestrationConstructionParam() : base("SimpleDragon Fenestration Construction", "Fenestration", "A SimpleDragon window or door construction.") { }
    public override Guid ComponentGuid => new("7b9bc26e-4c53-4955-abad-026293dc5e63");
}

public sealed class SimpleDragonUsageProfileParam : SimpleDragonParam<Types.SimpleDragonUsageProfileGoo>
{
    public SimpleDragonUsageProfileParam() : base("SimpleDragon Usage Profile", "Profile", "A SimpleDragon Korean usage profile.") { }
    public override Guid ComponentGuid => new("92fc684f-db5d-4163-87f0-d106e5d6e279");
}

public sealed class SimpleDragonSurfaceParam : SimpleDragonParam<Types.SimpleDragonSurfaceGoo>
{
    public SimpleDragonSurfaceParam() : base("SimpleDragon Surface", "Surface", "An area-and-azimuth SimpleDragon surface.") { }
    public override Guid ComponentGuid => new("4301600a-bca5-44a9-871f-0185fee5136e");
}

public sealed class SimpleDragonZoneParam : SimpleDragonParam<Types.SimpleDragonZoneGoo>
{
    public SimpleDragonZoneParam() : base("SimpleDragon Zone", "Zone", "An area-based SimpleDragon thermal zone.") { }
    public override Guid ComponentGuid => new("aa522b2d-9eac-47bc-885d-202d6d1741f4");
}

public sealed class SimpleDragonOpeningDefinitionParam
    : SimpleDragonParam<Types.SimpleDragonOpeningDefinitionGoo>
{
    public SimpleDragonOpeningDefinitionParam()
        : base(
            "SimpleDragon Opening Definition",
            "Opening",
            "A geometry-backed opening connected directly to its owning SimpleDragon zone.") { }
    public override Guid ComponentGuid => new("51610fe9-ecf1-43b4-9157-7260b3ba89ad");
}

public sealed class SimpleDragonZoneDefinitionParam
    : SimpleDragonParam<Types.SimpleDragonZoneDefinitionGoo>
{
    public SimpleDragonZoneDefinitionParam()
        : base(
            "SimpleDragon Zone Definition",
            "Zone Definition",
            "A geometry-backed SimpleDragon zone with its openings, usage, and HVAC inputs.") { }
    public override Guid ComponentGuid => new("3fe45962-67fe-43d4-be95-ad81b91b19eb");
}

public sealed class SimpleDragonSourceSystemParam : SimpleDragonParam<Types.SimpleDragonSourceSystemGoo>
{
    public SimpleDragonSourceSystemParam()
        : base("SimpleDragon Source System", "Source", "A SimpleDragon HVAC source system.") { }
    public override Guid ComponentGuid => new("11dead46-9ee4-48ce-913e-50ff7f10d319");
}

public sealed class SimpleDragonSupplySystemParam : SimpleDragonParam<Types.SimpleDragonSupplySystemGoo>
{
    public SimpleDragonSupplySystemParam()
        : base("SimpleDragon Supply System", "Supply", "A SimpleDragon zone HVAC supply system.") { }
    public override Guid ComponentGuid => new("51b809c1-a4ae-4dc7-bca8-81e06d49a806");
}

public sealed class SimpleDragonEnergyRecoveryVentilatorParam
    : SimpleDragonParam<Types.SimpleDragonEnergyRecoveryVentilatorGoo>
{
    public SimpleDragonEnergyRecoveryVentilatorParam()
        : base("SimpleDragon Energy Recovery Ventilator", "ERV", "A SimpleDragon energy-recovery ventilator.") { }
    public override Guid ComponentGuid => new("4a980e9a-a954-47c0-a34b-2026eb86b2ad");
}

public sealed class SimpleDragonVentilationAssignmentParam
    : SimpleDragonParam<Types.SimpleDragonVentilationAssignmentGoo>
{
    public SimpleDragonVentilationAssignmentParam()
        : base(
            "SimpleDragon Ventilation Assignment",
            "Ventilation",
            "A SimpleDragon energy-recovery ventilator assignment with a unit count.") { }
    public override Guid ComponentGuid => new("14f1683e-4b0a-4754-aac5-6b85331c2126");
}

public sealed class SimpleDragonPhotovoltaicPanelParam
    : SimpleDragonParam<Types.SimpleDragonPhotovoltaicPanelGoo>
{
    public SimpleDragonPhotovoltaicPanelParam()
        : base("SimpleDragon Photovoltaic Panel", "PV", "A SimpleDragon photovoltaic panel.") { }
    public override Guid ComponentGuid => new("731f38e6-55dd-4d1e-b9cb-ae33faf23154");
}

public sealed class GreenRetrofitModelParam : SimpleDragonParam<Types.GreenRetrofitModelGoo>
{
    public GreenRetrofitModelParam() : base("SimpleDragon GRM", "GRM", "A complete GRM 0.7 SimpleDragon model.") { }
    public override Guid ComponentGuid => new("e0546c97-2fba-4c51-9613-340dfb1fc416");
}

public sealed class GreenRetrofitResultParam : SimpleDragonParam<Types.GreenRetrofitResultGoo>
{
    public GreenRetrofitResultParam() : base("SimpleDragon GRR", "GRR", "A complete GRR 0.7 SimpleDragon result.") { }
    public override Guid ComponentGuid => new("c6431ff5-153b-49e7-a06b-26d3b91fbf6e");
}
