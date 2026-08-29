using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

/// <summary>
/// Directly composes one model alternative with its optional stable batch identity.
/// </summary>
public sealed class SimpleDragonBatchCaseComponent : SimpleDragonComponent
{
    public SimpleDragonBatchCaseComponent()
        : base(
            "SimpleDragon Batch Case",
            "SD Batch Case",
            "Creates one typed batch case from a GRM model and optional stable case ID. Runtime and weather remain module-managed.",
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
        pManager.AddTextParameter(
            "Case ID",
            "ID",
            "Optional stable case ID. Leave empty to derive a deterministic ID from the model.",
            GH_ParamAccess.item,
            string.Empty);
        pManager[1].Optional = true;
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
        string caseId = string.Empty;
        if (!DA.GetData(0, ref modelGoo) || modelGoo?.Value is null)
        {
            return;
        }

        DA.GetData(1, ref caseId);
        DA.SetData(
            0,
            new SimpleDragonBatchCaseGoo(new SimpleDragonBatchCase(modelGoo.Value, caseId)));
    }
}
