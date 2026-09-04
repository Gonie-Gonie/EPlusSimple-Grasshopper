using Dragons.BuildingEnergy.Contracts;

namespace Dragons.SimpleDragon.Grasshopper.Types;

public sealed class SimpleDragonDiagnosticGoo : SimpleDragonGoo<Diagnostic>
{
    public SimpleDragonDiagnosticGoo() { }
    public SimpleDragonDiagnosticGoo(Diagnostic value) : base(value) { }
    public override string TypeName => "SimpleDragon Diagnostic";
    public override string TypeDescription => "A stable SimpleDragon validation or execution diagnostic.";
    protected override SimpleDragonGoo<Diagnostic> Create(Diagnostic value) =>
        new SimpleDragonDiagnosticGoo(value);
    protected override SimpleDragonGoo<Diagnostic> CreateEmpty() => new SimpleDragonDiagnosticGoo();
    protected override string DisplayText(Diagnostic value) =>
        $"{value.Severity}: {value.Code} - {value.Message}";
}

public sealed class SimpleDragonMaterialGoo : SimpleDragonGoo<Material>
{
    public SimpleDragonMaterialGoo() { }
    public SimpleDragonMaterialGoo(Material value) : base(value) { }
    public override string TypeName => "SimpleDragon Material";
    public override string TypeDescription => "A SimpleDragon opaque material.";
    protected override SimpleDragonGoo<Material> Create(Material value) => new SimpleDragonMaterialGoo(value);
    protected override SimpleDragonGoo<Material> CreateEmpty() => new SimpleDragonMaterialGoo();
    protected override string DisplayText(Material value) => $"Material {value.Name}";
}

public sealed class SimpleDragonSurfaceConstructionLayerGoo
    : SimpleDragonGoo<SurfaceConstructionLayer>
{
    public SimpleDragonSurfaceConstructionLayerGoo() { }
    public SimpleDragonSurfaceConstructionLayerGoo(SurfaceConstructionLayer value) : base(value) { }
    public override string TypeName => "SimpleDragon Construction Layer";
    public override string TypeDescription =>
        "One opaque material and thickness, ready to be ordered in a surface construction.";
    protected override SimpleDragonGoo<SurfaceConstructionLayer> Create(
        SurfaceConstructionLayer value) => new SimpleDragonSurfaceConstructionLayerGoo(value);
    protected override SimpleDragonGoo<SurfaceConstructionLayer> CreateEmpty() =>
        new SimpleDragonSurfaceConstructionLayerGoo();
    protected override string DisplayText(SurfaceConstructionLayer value) =>
        $"Layer {value.Material.Name} ({value.Thickness:0.###} m)";
}

public sealed class SimpleDragonSurfaceConstructionGoo : SimpleDragonGoo<SurfaceConstruction>
{
    public SimpleDragonSurfaceConstructionGoo() { }
    public SimpleDragonSurfaceConstructionGoo(SurfaceConstruction value) : base(value) { }
    public override string TypeName => "SimpleDragon Surface Construction";
    public override string TypeDescription => "A layered SimpleDragon opaque construction.";
    protected override SimpleDragonGoo<SurfaceConstruction> Create(SurfaceConstruction value) => new SimpleDragonSurfaceConstructionGoo(value);
    protected override SimpleDragonGoo<SurfaceConstruction> CreateEmpty() => new SimpleDragonSurfaceConstructionGoo();
    protected override string DisplayText(SurfaceConstruction value) => $"Construction {value.Name} (U {value.GetUValue():0.###})";
}

public sealed class SimpleDragonFenestrationConstructionGoo : SimpleDragonGoo<FenestrationConstruction>
{
    public SimpleDragonFenestrationConstructionGoo() { }
    public SimpleDragonFenestrationConstructionGoo(FenestrationConstruction value) : base(value) { }
    public override string TypeName => "SimpleDragon Fenestration Construction";
    public override string TypeDescription => "A SimpleDragon window or door construction.";
    protected override SimpleDragonGoo<FenestrationConstruction> Create(FenestrationConstruction value) => new SimpleDragonFenestrationConstructionGoo(value);
    protected override SimpleDragonGoo<FenestrationConstruction> CreateEmpty() => new SimpleDragonFenestrationConstructionGoo();
    protected override string DisplayText(FenestrationConstruction value) => $"Fenestration {value.Name} (U {value.UValue:0.###})";
}

public sealed class SimpleDragonFenestrationGoo : SimpleDragonGoo<Fenestration>
{
    public SimpleDragonFenestrationGoo() { }
    public SimpleDragonFenestrationGoo(Fenestration value) : base(value) { }
    public override string TypeName => "SimpleDragon Fenestration";
    public override string TypeDescription => "A SimpleDragon window, glass door, or opaque door.";
    protected override SimpleDragonGoo<Fenestration> Create(Fenestration value) =>
        new SimpleDragonFenestrationGoo(value);
    protected override SimpleDragonGoo<Fenestration> CreateEmpty() => new SimpleDragonFenestrationGoo();
    protected override string DisplayText(Fenestration value) =>
        $"{value.Type} {value.Name} ({value.Area:0.###} m\u00B2)";
}

public sealed class SimpleDragonUsageProfileGoo : SimpleDragonGoo<UsageProfile>
{
    public SimpleDragonUsageProfileGoo() { }
    public SimpleDragonUsageProfileGoo(UsageProfile value) : base(value) { }
    public override string TypeName => "SimpleDragon Usage Profile";
    public override string TypeDescription => "A packaged or extended Korean usage profile.";
    protected override SimpleDragonGoo<UsageProfile> Create(UsageProfile value) => new SimpleDragonUsageProfileGoo(value);
    protected override SimpleDragonGoo<UsageProfile> CreateEmpty() => new SimpleDragonUsageProfileGoo();
    protected override string DisplayText(UsageProfile value) => $"Profile {value.Name} ({value.Source})";
}

public sealed class SimpleDragonSurfaceGoo : SimpleDragonGoo<Surface>
{
    public SimpleDragonSurfaceGoo() { }
    public SimpleDragonSurfaceGoo(Surface value) : base(value) { }
    public override string TypeName => "SimpleDragon Surface";
    public override string TypeDescription => "An area-and-azimuth SimpleDragon surface.";
    protected override SimpleDragonGoo<Surface> Create(Surface value) => new SimpleDragonSurfaceGoo(value);
    protected override SimpleDragonGoo<Surface> CreateEmpty() => new SimpleDragonSurfaceGoo();
    protected override string DisplayText(Surface value) => $"{value.Type} {value.Name} ({value.Area:0.###} m\u00B2)";
}

public sealed class SimpleDragonZoneGoo : SimpleDragonGoo<Zone>
{
    public SimpleDragonZoneGoo() { }
    public SimpleDragonZoneGoo(Zone value) : base(value) { }
    public override string TypeName => "SimpleDragon Zone";
    public override string TypeDescription => "A SimpleDragon area-based thermal zone.";
    protected override SimpleDragonGoo<Zone> Create(Zone value) => new SimpleDragonZoneGoo(value);
    protected override SimpleDragonGoo<Zone> CreateEmpty() => new SimpleDragonZoneGoo();
    protected override string DisplayText(Zone value) => $"Zone {value.Name} ({value.Surfaces.Count} surfaces, {value.Area:0.###} m\u00B2)";
}

public sealed class SimpleDragonSourceSystemGoo : SimpleDragonGoo<SourceSystem>
{
    public SimpleDragonSourceSystemGoo() { }
    public SimpleDragonSourceSystemGoo(SourceSystem value) : base(value) { }
    public override string TypeName => "SimpleDragon Source System";
    public override string TypeDescription => "A SimpleDragon HVAC source system.";
    protected override SimpleDragonGoo<SourceSystem> Create(SourceSystem value) => new SimpleDragonSourceSystemGoo(value);
    protected override SimpleDragonGoo<SourceSystem> CreateEmpty() => new SimpleDragonSourceSystemGoo();
    protected override string DisplayText(SourceSystem value) => $"Source {value.Name} ({value.Type})";
}

public sealed class SimpleDragonSupplySystemGoo : SimpleDragonGoo<SupplySystem>
{
    public SimpleDragonSupplySystemGoo() { }
    public SimpleDragonSupplySystemGoo(SupplySystem value) : base(value) { }
    public override string TypeName => "SimpleDragon Supply System";
    public override string TypeDescription => "A SimpleDragon zone HVAC supply system.";
    protected override SimpleDragonGoo<SupplySystem> Create(SupplySystem value) => new SimpleDragonSupplySystemGoo(value);
    protected override SimpleDragonGoo<SupplySystem> CreateEmpty() => new SimpleDragonSupplySystemGoo();
    protected override string DisplayText(SupplySystem value) => $"Supply {value.Name} ({value.Type})";
}

public sealed class SimpleDragonPhotovoltaicPanelGoo : SimpleDragonGoo<PhotovoltaicSystem>
{
    public SimpleDragonPhotovoltaicPanelGoo() { }
    public SimpleDragonPhotovoltaicPanelGoo(PhotovoltaicSystem value) : base(value) { }
    public override string TypeName => "SimpleDragon Photovoltaic Panel";
    public override string TypeDescription => "A SimpleDragon photovoltaic panel.";
    protected override SimpleDragonGoo<PhotovoltaicSystem> Create(PhotovoltaicSystem value) =>
        new SimpleDragonPhotovoltaicPanelGoo(value);
    protected override SimpleDragonGoo<PhotovoltaicSystem> CreateEmpty() =>
        new SimpleDragonPhotovoltaicPanelGoo();
    protected override string DisplayText(PhotovoltaicSystem value) =>
        $"Photovoltaic Panel {value.Name} ({value.Area:0.###} m\u00B2)";
}

public sealed class GreenRetrofitModelGoo : SimpleDragonGeometryContextGoo<GreenRetrofitModel>
{
    public GreenRetrofitModelGoo() { }

    public GreenRetrofitModelGoo(
        GreenRetrofitModel value,
        IEnumerable<GreenRetrofitGeometryMapEntry>? geometryMap = null)
        : base(value, geometryMap)
    { }

    public override string TypeName => "SimpleDragon GRM";
    public override string TypeDescription => "A complete GRM 0.7 SimpleDragon model.";
    protected override SimpleDragonGoo<GreenRetrofitModel> Create(GreenRetrofitModel value) =>
        new GreenRetrofitModelGoo(value, GeometryMap);
    protected override SimpleDragonGoo<GreenRetrofitModel> CreateEmpty() => new GreenRetrofitModelGoo();
    protected override string DisplayText(GreenRetrofitModel value) => $"GRM {value.Name} ({value.Zones.Count} zones, {value.Area:0.###} m\u00B2)";
}

public sealed class GreenRetrofitResultGoo : SimpleDragonGeometryContextGoo<GreenRetrofitResult>
{
    public GreenRetrofitResultGoo() { }

    public GreenRetrofitResultGoo(
        GreenRetrofitResult value,
        IEnumerable<GreenRetrofitGeometryMapEntry>? geometryMap = null)
        : base(value, geometryMap)
    { }

    public override string TypeName => "SimpleDragon GRR";
    public override string TypeDescription => "A complete GRR 0.7 SimpleDragon result.";
    protected override SimpleDragonGoo<GreenRetrofitResult> Create(GreenRetrofitResult value) =>
        new GreenRetrofitResultGoo(value, GeometryMap);
    protected override SimpleDragonGoo<GreenRetrofitResult> CreateEmpty() => new GreenRetrofitResultGoo();
    protected override string DisplayText(GreenRetrofitResult value) => $"GRR ({value.TotalArea:0.###} m\u00B2)";
}
