using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

public sealed class SimpleDragonMaterialComponent : SimpleDragonComponent
{
    public SimpleDragonMaterialComponent()
        : base(
            "SimpleDragon Material",
            "SD Material",
            "Creates a SimpleDragon material using SI thermophysical properties.",
            SimpleDragonPanels.Construction)
    {
    }

    public override Guid ComponentGuid => new("fee586e8-692c-407e-a803-d5c43f3c7222");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Material name.", GH_ParamAccess.item, "Simple Material");
        pManager.AddNumberParameter("Conductivity", "k", "Conductivity in W/(m K).", GH_ParamAccess.item, 0.04);
        pManager.AddNumberParameter("Density", "ρ", "Density in kg/m\u00B3.", GH_ParamAccess.item, 30);
        pManager.AddNumberParameter("Specific Heat", "Cp", "Specific heat in J/(kg K).", GH_ParamAccess.item, 1400);
        pManager.AddTextParameter("ID", "ID", "Optional stable identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonMaterialParam(), "Material", "M", "SimpleDragon material.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Simple Material";
        double conductivity = 0.04;
        double density = 30;
        double specificHeat = 1400;
        string id = string.Empty;
        if (!DA.GetData(0, ref name)
            || !DA.GetData(1, ref conductivity)
            || !DA.GetData(2, ref density)
            || !DA.GetData(3, ref specificHeat))
        {
            return;
        }

        DA.GetData(4, ref id);
        var material = new Material(
            name,
            conductivity,
            density,
            specificHeat,
            string.IsNullOrWhiteSpace(id) ? null : new EntityId(id.Trim()));
        DA.SetData(0, new SimpleDragonMaterialGoo(material));
    }
}

public sealed class SimpleDragonSurfaceConstructionLayerComponent : SimpleDragonComponent
{
    public SimpleDragonSurfaceConstructionLayerComponent()
        : base(
            "SimpleDragon Construction Layer",
            "SD Layer",
            "Combines one opaque material with its thickness for direct construction composition.",
            SimpleDragonPanels.Construction)
    {
    }

    public override Guid ComponentGuid => new("b97da4a1-7b1c-472a-a4b0-83603e202c2b");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(
            new SimpleDragonMaterialParam(),
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
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new SimpleDragonSurfaceConstructionLayerParam(),
            "Layer",
            "L",
            "Typed SimpleDragon construction layer.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        SimpleDragonMaterialGoo? materialGoo = null;
        double thickness = 0.1d;
        if (!DA.GetData(0, ref materialGoo)
            || !DA.GetData(1, ref thickness)
            || materialGoo?.Value is null)
        {
            return;
        }

        DA.SetData(
            0,
            new SimpleDragonSurfaceConstructionLayerGoo(
                new SurfaceConstructionLayer(materialGoo.Value, thickness)));
    }
}

public sealed class SimpleDragonSurfaceConstructionComponent : SimpleDragonComponent
{
    public SimpleDragonSurfaceConstructionComponent()
        : base(
            "SimpleDragon Surface Construction",
            "SD Construction",
            "Creates a layered SimpleDragon surface construction from directly connected layers.",
            SimpleDragonPanels.Construction)
    {
    }

    public override Guid ComponentGuid => new("3e1fa67f-dbb2-4c19-b54b-226c295f5751");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Construction name.", GH_ParamAccess.item, "Simple Construction");
        pManager.AddParameter(
            new SimpleDragonSurfaceConstructionLayerParam(),
            "Layers",
            "L",
            "Construction layers in SimpleDragon database order.",
            GH_ParamAccess.list);
        pManager.AddTextParameter("ID", "ID", "Optional stable identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new SimpleDragonSurfaceConstructionParam(),
            "Construction",
            "C",
            "SimpleDragon surface construction.",
            GH_ParamAccess.item);
        pManager.AddNumberParameter("U-Value", "U", "U-value including default films in W/(m\u00B2 K).", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Simple Construction";
        var layerGoos = new List<SimpleDragonSurfaceConstructionLayerGoo>();
        string id = string.Empty;
        if (!DA.GetData(0, ref name)
            || !DA.GetDataList(1, layerGoos))
        {
            return;
        }

        DA.GetData(2, ref id);
        SurfaceConstructionLayer[] layers = layerGoos.Select((goo, index) =>
        {
            if (goo?.Value is null)
            {
                throw new ArgumentException("Layer at position " + (index + 1) + " is empty.");
            }

            return goo.Value;
        }).ToArray();
        var construction = new SurfaceConstruction(
            name,
            layers,
            string.IsNullOrWhiteSpace(id) ? null : new EntityId(id.Trim()));
        DA.SetData(0, new SimpleDragonSurfaceConstructionGoo(construction));
        DA.SetData(1, construction.GetUValue());
    }
}

public sealed class SimpleDragonFenestrationConstructionComponent : SimpleDragonComponent
{
    public SimpleDragonFenestrationConstructionComponent()
        : base(
            "SimpleDragon Fenestration Construction",
            "SD Fenestration",
            "Creates a SimpleDragon transparent window or opaque door construction.",
            SimpleDragonPanels.Construction)
    {
    }

    public override Guid ComponentGuid => new("b9af07b4-d08e-4335-ab55-a6fd33cb1a93");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Fenestration construction name.", GH_ParamAccess.item, "Simple Window");
        pManager.AddNumberParameter("U-Value", "U", "U-value in W/(m\u00B2 K).", GH_ParamAccess.item, 1.5);
        pManager.AddNumberParameter("SHGC", "g", "Solar heat gain coefficient. Set zero for an opaque door.", GH_ParamAccess.item, 0.5);
        pManager.AddTextParameter("ID", "ID", "Optional stable identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new SimpleDragonFenestrationConstructionParam(),
            "Construction",
            "C",
            "SimpleDragon fenestration construction.",
            GH_ParamAccess.item);
        pManager.AddBooleanParameter("Transparent", "T", "True when this construction is for windows or glass doors.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Simple Window";
        double uValue = 1.5;
        double solarGain = 0.5;
        string id = string.Empty;
        if (!DA.GetData(0, ref name)
            || !DA.GetData(1, ref uValue)
            || !DA.GetData(2, ref solarGain))
        {
            return;
        }

        DA.GetData(3, ref id);
        var construction = new FenestrationConstruction(
            name,
            uValue,
            solarGain == 0 ? null : solarGain,
            string.IsNullOrWhiteSpace(id) ? null : new EntityId(id.Trim()));
        DA.SetData(0, new SimpleDragonFenestrationConstructionGoo(construction));
        DA.SetData(1, construction.IsTransparent);
    }
}

public sealed class LookupUsageProfileComponent : SimpleDragonComponent
{
    public LookupUsageProfileComponent()
        : base(
            "Lookup SimpleDragon Usage Profile",
            "SD Profile",
            "Looks up a packaged Korean standard or extended usage profile by exact name.",
            SimpleDragonPanels.Construction)
    {
    }

    public override Guid ComponentGuid => new("fb92c938-41e1-475f-ad03-ca6a1a8e42e1");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Exact packaged usage-profile name. Leave empty to list names.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonUsageProfileParam(), "Profile", "P", "Resolved usage profile.", GH_ParamAccess.item);
        pManager.AddTextParameter("Available Names", "Names", "All packaged usage-profile names.", GH_ParamAccess.list);
        pManager.AddParameter(new SimpleDragonDiagnosticParam(), "Diagnostics", "D", "Lookup diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = string.Empty;
        DA.GetData(0, ref name);
        UsageProfileDatabase database = SimpleDragonDatabase.Default.UsageProfiles;
        DA.SetDataList(1, database.Items.Select(item => item.Name));
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        LookupResult<UsageProfile> lookup = database.Find(name);
        Report(lookup.Diagnostics);
        if (lookup.Value is not null)
        {
            DA.SetData(0, new SimpleDragonUsageProfileGoo(lookup.Value));
        }

        DA.SetDataList(2, lookup.Diagnostics.Select(item => new SimpleDragonDiagnosticGoo(item)));
    }
}
