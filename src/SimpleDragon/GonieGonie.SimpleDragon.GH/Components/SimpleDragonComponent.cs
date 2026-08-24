using GonieGonie.BuildingEnergy.Contracts;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

internal static class SimpleDragonPanels
{
    internal const string Category = "SimpleDragon";
    internal const string Core = "Core";
    internal const string Construction = "Construction";
    internal const string Geometry = "Geometry";
    internal const string Model = "Model";
    internal const string Results = "Results";
}

public abstract class SimpleDragonComponent : GH_Component
{
    protected SimpleDragonComponent(
        string name,
        string nickname,
        string description,
        string subcategory)
        : base(name, nickname, description, SimpleDragonPanels.Category, subcategory)
    {
    }

    protected override Bitmap? Icon => PluginIcons.Icon24;

    protected sealed override void SolveInstance(IGH_DataAccess DA)
    {
        try
        {
            Solve(DA);
        }
        catch (Exception exception)
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                exception.GetType().Name + ": " + exception.Message);
        }
    }

    protected abstract void Solve(IGH_DataAccess DA);

    protected string ResolveDocumentPath(string path)
    {
        return GrasshopperDocumentPathResolver.Resolve(path, OnPingDocument());
    }

    protected void Report(IEnumerable<Diagnostic> diagnostics)
    {
        foreach (Diagnostic diagnostic in diagnostics)
        {
            GH_RuntimeMessageLevel level = diagnostic.Severity switch
            {
                DiagnosticSeverity.Info => GH_RuntimeMessageLevel.Remark,
                DiagnosticSeverity.Warning => GH_RuntimeMessageLevel.Warning,
                _ => GH_RuntimeMessageLevel.Error,
            };
            AddRuntimeMessage(level, diagnostic.Code + ": " + diagnostic.Message);
        }
    }
}
