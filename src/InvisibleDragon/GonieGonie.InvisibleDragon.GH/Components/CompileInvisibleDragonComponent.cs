using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using Grasshopper.Kernel;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

/// <summary>
/// Canonical path-free InvisibleDragon model compiler.
/// </summary>
public sealed class CompileInvisibleDragonComponent : DragonComponent
{
    public CompileInvisibleDragonComponent()
        : base(
            "Compile InvisibleDragon",
            "ID to IDF",
            "Compiles an InvisibleDragon model for EnergyPlus 24.2. The managed IDD and embedded execution mapping are resolved internally; no path input is required.",
            DragonPanels.Model)
    {
    }

    public override Guid ComponentGuid => new("e3e4d8f9-4fd8-4b17-9ec7-a27cb5627802");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonEnergyModelParam(),
            "Model",
            "M",
            "InvisibleDragon energy model.",
            GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonIdfParam(),
            "IDF",
            "IDF",
            "EnergyPlus 24.2 execution document.",
            GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Text",
            "T",
            "Deterministic IDF text.",
            GH_ParamAccess.item);
        pManager.AddBooleanParameter(
            "Valid",
            "V",
            "True when model validation and any available managed-IDD validation pass.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new DiagnosticParam(),
            "Diagnostics",
            "D",
            "Compilation diagnostics.",
            GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        DragonEnergyModelGoo? modelGoo = null;
        if (!DA.GetData(0, ref modelGoo))
        {
            return;
        }

        if (modelGoo?.Value is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Model is required.");
            return;
        }

        try
        {
            EnergyModel model = modelGoo.Value;
            var options = new EnergyModelIdfOptions { ThrowOnValidationErrors = false };
            var diagnostics = model.Validate().Diagnostics.ToList();
            IddSchema? managedSchema = IddSchemaProvider.Resolve(null);
            IdfDocument document;
            if (managedSchema is null)
            {
                document = EnergyPlus242ExecutionIdfBuilder.Create(model, options);
                diagnostics.Add(new Diagnostic(
                    "INVISIBLEDRAGON.GH.MANAGED_IDD_DEFERRED",
                    DiagnosticSeverity.Info,
                    "The embedded EnergyPlus 24.2 execution mapping was used; the full managed IDD will be available when the bundled runtime is prepared.",
                    suggestedAction: "Connect the typed IDF and SimpleDragon Weather outputs to Run InvisibleDragon."));
            }
            else
            {
                document = model.ToIdfDocument(managedSchema, options);
                diagnostics.AddRange(IdfValidator.Validate(document).Diagnostics);
            }

            bool valid = !diagnostics.Any(item => item.IsFailure);
            Report(diagnostics);
            DA.SetData(0, new DragonIdfGoo(document));
            DA.SetData(1, IdfWriter.Write(document));
            DA.SetData(2, valid);
            DA.SetDataList(3, diagnostics.Select(item => new DiagnosticGoo(item)));
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, exception.Message);
            DA.SetData(2, false);
            DA.SetDataList(
                3,
                new[]
                {
                    new DiagnosticGoo(new Diagnostic(
                        "INVISIBLEDRAGON.GH.COMPILE_FAILED",
                        DiagnosticSeverity.Error,
                        exception.Message,
                        suggestedAction: "Correct the connected model and recompute the component.")),
                });
        }
    }
}
