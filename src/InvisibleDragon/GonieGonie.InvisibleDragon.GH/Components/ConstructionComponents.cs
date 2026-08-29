using Grasshopper.Kernel;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using System.Globalization;
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

public sealed class ConstructionLayerComponent : DragonComponent
{
    public ConstructionLayerComponent()
        : base(
            "Construction Layer",
            "Layer",
            "Combines one opaque material with its thickness for direct construction composition.",
            DragonPanels.Construction)
    {
    }

    public override Guid ComponentGuid => new("d15984d5-cd3f-4798-a67c-73138b54859e");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonMaterialParam(),
            "Material",
            "M",
            "Opaque material owned by this layer.",
            GH_ParamAccess.item);
        pManager.AddNumberParameter(
            "Thickness",
            "T",
            "Layer thickness in metres.",
            GH_ParamAccess.item,
            0.1d);
        pManager.AddTextParameter(
            "Name",
            "N",
            "Optional layer name. Blank generates a stable descriptive name.",
            GH_ParamAccess.item,
            string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonLayerParam(),
            "Layer",
            "L",
            "Typed construction layer.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        DragonMaterialGoo? materialGoo = null;
        double thickness = 0.1d;
        string name = string.Empty;
        if (!DA.GetData(0, ref materialGoo)
            || !DA.GetData(1, ref thickness)
            || materialGoo?.Value is null)
        {
            return;
        }

        DA.GetData(2, ref name);
        string resolvedName = string.IsNullOrWhiteSpace(name)
            ? materialGoo.Value.Name + ":" + thickness.ToString("0.########", CultureInfo.InvariantCulture) + "m"
            : name.Trim();
        DA.SetData(0, new DragonLayerGoo(new Layer(resolvedName, materialGoo.Value, thickness)));
    }
}

public sealed class LayeredConstructionComponent : DragonComponent
{
    public LayeredConstructionComponent()
        : base(
            "Layered Construction",
            "Con",
            "Creates an outside-to-inside opaque construction from directly connected layers.",
            DragonPanels.Construction)
    {
    }

    public override Guid ComponentGuid => new("6d5a9b54-8a9e-4c95-91df-469e21a783c9");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Construction name.", GH_ParamAccess.item, "Layered Construction");
        pManager.AddParameter(
            new DragonLayerParam(),
            "Layers",
            "L",
            "Construction layers ordered from outside to inside.",
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
        var layerGoos = new List<DragonLayerGoo>();
        if (!DA.GetData(0, ref name) ||
            !DA.GetDataList(1, layerGoos))
        {
            return;
        }

        Layer[] layers = layerGoos.Select((goo, index) =>
        {
            if (goo?.Value is null)
            {
                throw new ArgumentException($"Layer at position {index + 1} is empty.");
            }

            return goo.Value;
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
