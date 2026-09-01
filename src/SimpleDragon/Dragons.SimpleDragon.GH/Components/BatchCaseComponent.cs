using Dragons.SimpleDragon.Grasshopper.Parameters;
using Dragons.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace Dragons.SimpleDragon.Grasshopper.Components;

/// <summary>
/// Directly composes one model alternative. The batch runner derives its stable
/// execution identity internally from the model and its input order.
/// </summary>
public sealed class SimpleDragonBatchCaseComponent : SimpleDragonComponent
{
    public SimpleDragonBatchCaseComponent()
        : base(
            "SimpleDragon Batch Case",
            "SD Batch Case",
            "Creates one typed batch case from a GRM model. Identity, runtime, and weather remain module-managed.",
            SimpleDragonPanels.Results)
    {
    }

    public override Guid ComponentGuid => new("11336c6a-5bd4-4d6b-80a1-89bd168f8d54");

    public override GH_Exposure Exposure => GH_Exposure.primary;

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(
            new GreenRetrofitModelParam(),
            "GRM",
            "GRM",
            "One complete SimpleDragon model alternative.",
            GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new SimpleDragonBatchCaseParam(),
            "Case",
            "Case",
            "Typed SimpleDragon batch case.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        GreenRetrofitModelGoo? modelGoo = null;
        if (!DA.GetData(0, ref modelGoo) || modelGoo?.Value is null)
        {
            return;
        }

        DA.SetData(
            0,
            new SimpleDragonBatchCaseGoo(new SimpleDragonBatchCase(modelGoo.Value)));
    }
}
