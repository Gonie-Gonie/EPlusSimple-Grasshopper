using Grasshopper.Kernel;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

public sealed class OpaqueMaterialComponent : DragonComponent
{
    public OpaqueMaterialComponent()
        : base(
            "Opaque Material",
            "Mat",
            "Creates an immutable opaque material using SI thermophysical properties.",
            DragonPanels.Construction)
    {
    }

    public override Guid ComponentGuid => new("dca742da-0ac5-4520-8022-97f98974dfea");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Material name.", GH_ParamAccess.item, "Opaque Material");
        pManager.AddNumberParameter("Conductivity", "k", "Conductivity in W/(m K).", GH_ParamAccess.item, 0.5);
        pManager.AddNumberParameter("Density", "ρ", "Density in kg/m³.", GH_ParamAccess.item, 800);
        pManager.AddNumberParameter("Specific Heat", "Cp", "Specific heat in J/(kg K).", GH_ParamAccess.item, 1000);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonMaterialParam(),
            "Material",
            "M",
            "InvisibleDragon material.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Opaque Material";
        double conductivity = 0.5;
        double density = 800;
        double specificHeat = 1000;
        if (!DA.GetData(0, ref name) ||
            !DA.GetData(1, ref conductivity) ||
            !DA.GetData(2, ref density) ||
            !DA.GetData(3, ref specificHeat))
        {
            return;
        }

        DA.SetData(0, new DragonMaterialGoo(new Material(name, conductivity, density, specificHeat)));
    }
}

public sealed class LayeredConstructionComponent : DragonComponent
{
    public LayeredConstructionComponent()
        : base(
            "Layered Construction",
            "Con",
            "Creates an outside-to-inside opaque construction from materials and layer thicknesses.",
            DragonPanels.Construction)
    {
    }

    public override Guid ComponentGuid => new("6d5a9b54-8a9e-4c95-91df-469e21a783c9");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Construction name.", GH_ParamAccess.item, "Layered Construction");
        pManager.AddParameter(
            new DragonMaterialParam(),
            "Materials",
            "M",
            "Materials ordered from outside to inside.",
            GH_ParamAccess.list);
        pManager.AddNumberParameter(
            "Thicknesses",
            "T",
            "Layer thicknesses in metres, in the same order as Materials.",
            GH_ParamAccess.list);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonConstructionParam(),
            "Construction",
            "C",
            "InvisibleDragon layered construction.",
            GH_ParamAccess.item);
        pManager.AddNumberParameter("U-Value", "U", "Calculated U-value in W/(m² K).", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Layered Construction";
        var materials = new List<DragonMaterialGoo>();
        var thicknesses = new List<double>();
        if (!DA.GetData(0, ref name) ||
            !DA.GetDataList(1, materials) ||
            !DA.GetDataList(2, thicknesses))
        {
            return;
        }

        if (materials.Count != thicknesses.Count)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Materials and Thicknesses must have equal lengths.");
            return;
        }

        Layer[] layers = materials.Select((goo, index) =>
        {
            if (goo?.Value is null)
            {
                throw new ArgumentException($"Material at index {index} is empty.");
            }

            return new Layer($"{name}:Layer:{index + 1}", goo.Value, thicknesses[index]);
        }).ToArray();
        var construction = new OpaqueConstruction(name, layers);
        DA.SetData(0, new DragonConstructionGoo(construction));
        DA.SetData(1, construction.UValue);
    }
}

public sealed class NoMassConstructionComponent : DragonComponent
{
    public NoMassConstructionComponent()
        : base(
            "No-Mass Construction",
            "NoMass",
            "Creates a massless opaque construction from its U-value.",
            DragonPanels.Construction)
    {
    }

    public override Guid ComponentGuid => new("e292a44e-9d8d-4796-95fb-126f77e83796");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Construction name.", GH_ParamAccess.item, "No-Mass Construction");
        pManager.AddNumberParameter("U-Value", "U", "U-value in W/(m² K).", GH_ParamAccess.item, 0.35);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonConstructionParam(),
            "Construction",
            "C",
            "InvisibleDragon no-mass construction.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "No-Mass Construction";
        double uValue = 0.35;
        if (!DA.GetData(0, ref name) || !DA.GetData(1, ref uValue))
        {
            return;
        }

        DA.SetData(0, new DragonConstructionGoo(new NoMassConstruction(name, uValue)));
    }
}
