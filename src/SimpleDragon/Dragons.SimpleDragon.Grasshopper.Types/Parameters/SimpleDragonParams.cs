using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace Dragons.SimpleDragon.Grasshopper.Parameters;

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

public sealed class SimpleDragonDiagnosticParam
    : SimpleDragonParam<Types.SimpleDragonDiagnosticGoo>
{
    public SimpleDragonDiagnosticParam()
        : base(
            "SimpleDragon Diagnostic",
            "Diagnostic",
            "A stable SimpleDragon validation or execution diagnostic.") { }
    public override Guid ComponentGuid => new("e54751c3-4d56-4499-83fb-f833822cf6bb");
}

public sealed class SimpleDragonMaterialParam : SimpleDragonParam<Types.SimpleDragonMaterialGoo>
{
    public SimpleDragonMaterialParam() : base("SimpleDragon Material", "Material", "A SimpleDragon opaque material.") { }
    public override Guid ComponentGuid => new("f03481bf-f063-4991-b610-82af1d11aeae");
}

public sealed class SimpleDragonSurfaceConstructionLayerParam
    : SimpleDragonParam<Types.SimpleDragonSurfaceConstructionLayerGoo>
{
    public SimpleDragonSurfaceConstructionLayerParam()
        : base(
            "SimpleDragon Construction Layer",
            "Layer",
            "One material and thickness in a SimpleDragon opaque construction.") { }
    public override Guid ComponentGuid => new("06f57aae-c0dc-46f9-8af8-e9fa4429fcb7");
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

public sealed class SimpleDragonFenestrationParam
    : SimpleDragonParam<Types.SimpleDragonFenestrationGoo>
{
    public SimpleDragonFenestrationParam()
        : base(
            "SimpleDragon Fenestration",
            "Fenestration",
            "A SimpleDragon window, glass door, or opaque door.") { }
    public override Guid ComponentGuid => new("f79add66-5a0e-4c0b-b42e-8b02fb7e153f");
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
            "A geometry-backed opening connected directly to its owning SimpleDragon surface.") { }
    public override Guid ComponentGuid => new("51610fe9-ecf1-43b4-9157-7260b3ba89ad");
}

public sealed class SimpleDragonSurfaceDefinitionParam
    : SimpleDragonParam<Types.SimpleDragonSurfaceDefinitionGoo>
{
    public SimpleDragonSurfaceDefinitionParam()
        : base(
            "SimpleDragon Surface Definition",
            "Surface Definition",
            "A geometry-backed surface with its construction, boundary intent, and openings.") { }
    public override Guid ComponentGuid => new("14feee1f-498c-478c-92ac-4bd0e9d256da");
}

public sealed class SimpleDragonZoneDefinitionParam
    : SimpleDragonParam<Types.SimpleDragonZoneDefinitionGoo>
{
    public SimpleDragonZoneDefinitionParam()
        : base(
            "SimpleDragon Zone Definition",
            "Zone Definition",
            "A SimpleDragon zone composed from its owned surfaces, usage, and HVAC inputs.") { }
    public override Guid ComponentGuid => new("df2c89ba-56a7-48ea-83f2-ba58ac15f17f");
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

public sealed class SimpleDragonZoneErvParam
    : SimpleDragonParam<Types.SimpleDragonZoneErvGoo>
{
    public SimpleDragonZoneErvParam()
        : base(
            "SimpleDragon Zone ERV",
            "Zone ERV",
            "An ERV owned by a SimpleDragon Zone, including its positive unit count.") { }
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

public sealed class SimpleDragonBatchCaseParam
    : SimpleDragonParam<Types.SimpleDragonBatchCaseGoo>
{
    public SimpleDragonBatchCaseParam()
        : base(
            "SimpleDragon Batch Case",
            "Batch Case",
            "One SimpleDragon GRM alternative with its optional stable batch case ID.") { }
    public override Guid ComponentGuid => new("c30c8d9a-15bd-4dd1-b1dd-3d1d3a2d7169");
}

public sealed class GreenRetrofitResultParam : SimpleDragonParam<Types.GreenRetrofitResultGoo>
{
    public GreenRetrofitResultParam() : base("SimpleDragon GRR", "GRR", "A complete GRR 0.7 SimpleDragon result.") { }
    public override Guid ComponentGuid => new("c6431ff5-153b-49e7-a06b-26d3b91fbf6e");
}
