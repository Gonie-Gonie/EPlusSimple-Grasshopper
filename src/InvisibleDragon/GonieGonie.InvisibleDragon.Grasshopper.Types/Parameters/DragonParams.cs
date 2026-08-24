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

    protected override Bitmap? Icon => null;

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
