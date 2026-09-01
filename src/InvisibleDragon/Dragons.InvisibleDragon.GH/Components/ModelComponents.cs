using Grasshopper.Kernel;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Grasshopper.Parameters;
using Dragons.InvisibleDragon.Grasshopper.Types;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Shape;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

namespace Dragons.InvisibleDragon.Grasshopper.Components;

public sealed class ZoneComponent : DragonComponent
{
    public ZoneComponent()
        : base(
            "Thermal Zone",
            "Zone",
            "Collects one thermal zone with its surfaces, profile, HVAC, and energy-recovery ventilation. Connect owned systems directly to the Zone.",
            DragonPanels.Model)
    {
    }

    public override Guid ComponentGuid => new("21ece4e9-87dd-4f34-9b95-8bc87fb0bfd2");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Zone name.", GH_ParamAccess.item, "Zone");
        pManager.AddParameter(new DragonSurfaceParam(), "Surfaces", "S", "Closed boundary surfaces owned by this Zone.", GH_ParamAccess.list);
        pManager.AddParameter(new DragonProfileParam(), "Profile", "P", "Zone usage profile.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Infiltration", "ACH", "Infiltration in air changes per hour.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Lighting Power Density", "LPD", "Lighting power density in W/m².", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Outdoor Air Flow", "OA", "Outdoor air flow in m³/s.", GH_ParamAccess.item, 0);
        int supplies = pManager.AddParameter(
            new DragonSupplySystemParam(),
            "HVAC",
            "HVAC",
            "Supply systems owned by this Zone.",
            GH_ParamAccess.list);
        int ventilators = pManager.AddParameter(
            new DragonEnergyRecoveryVentilatorParam(),
            "ERVs",
            "ERV",
            "Energy-recovery ventilators owned by this Zone.",
            GH_ParamAccess.list);
        pManager[supplies].Optional = true;
        pManager[ventilators].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonZoneDefinitionParam(),
            "Zone",
            "Z",
            "InvisibleDragon Zone definition with directly owned HVAC and ERVs.",
            GH_ParamAccess.item);
        pManager.AddBooleanParameter("Valid", "V", "True when the Zone definition has no error diagnostics.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Zone and owned-system diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Zone";
        var surfaceGoos = new List<DragonSurfaceGoo>();
        DragonProfileGoo? profileGoo = null;
        double infiltration = 0;
        double lightingPowerDensity = 0;
        double outdoorAirFlow = 0;
        var supplyGoos = new List<DragonSupplySystemGoo>();
        var ventilatorGoos = new List<DragonEnergyRecoveryVentilatorGoo>();
        if (!DA.GetData(0, ref name)
            || !DA.GetDataList(1, surfaceGoos)
            || !DA.GetData(2, ref profileGoo)
            || !DA.GetData(3, ref infiltration)
            || !DA.GetData(4, ref lightingPowerDensity)
            || !DA.GetData(5, ref outdoorAirFlow))
        {
            return;
        }

        DA.GetDataList(6, supplyGoos);
        DA.GetDataList(7, ventilatorGoos);
        ZoneProfile profile = profileGoo?.Value
            ?? throw new ArgumentException("Profile requires a non-empty value.");
        Surface[] surfaces = surfaceGoos.Select((goo, index) => goo?.Value
            ?? throw new ArgumentException("Surfaces contains an empty value at position " + index + "."))
            .ToArray();
        SupplySystem[] supplies = supplyGoos
            .Select((goo, index) => HvacComponentSupport.Supply(goo, "HVAC", index))
            .ToArray();
        EnergyRecoveryVentilator[] ventilators = ventilatorGoos.Select((goo, index) => goo?.Value
            ?? throw new ArgumentException("ERVs contains an empty value at position " + index + "."))
            .ToArray();
        var zone = new Zone(
            StableIds.Create("zone", name, string.Join("|", surfaces.Select(item => item.Id.Value))),
            name,
            surfaces,
            profile,
            infiltration,
            lightingPowerDensity,
            outdoorAirFlow);
        var definition = new InvisibleDragonZoneDefinition(zone, supplies, ventilators: ventilators);
        ValidationResult validation = zone.Validate();
        Report(validation.Diagnostics);
        DA.SetData(0, new DragonZoneDefinitionGoo(definition));
        DA.SetData(1, validation.IsValid);
        DA.SetDataList(2, validation.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}

public sealed class EnergyModelComponent : DragonComponent
{
    public EnergyModelComponent()
        : base(
            "Energy Model",
            "Model",
            "Resolves interzone adjacency and composes Zone-owned HVAC and ventilation into a complete InvisibleDragon energy model.",
            DragonPanels.Model)
    {
    }

    public override Guid ComponentGuid => new("057ee08b-759f-43e0-8ab8-625747d951ef");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Model name.", GH_ParamAccess.item, "InvisibleDragon Model");
        pManager.AddParameter(
            new DragonZoneDefinitionParam(),
            "Zones",
            "Z",
            "Zone definitions. Coincident surfaces across distinct Zones are paired automatically.",
            GH_ParamAccess.list);
        pManager.AddNumberParameter("North Axis", "North", "North-axis rotation in degrees.", GH_ParamAccess.item, 0);
        ChoiceInputs.AddEnum(
            pManager,
            "Terrain",
            "T",
            "Site terrain used by the EnergyPlus model.",
            Terrain.Suburbs);
        int photovoltaicPanels = pManager.AddParameter(
            new DragonPhotovoltaicPanelParam(),
            "PV Panels",
            "PV",
            "Optional model-level photovoltaic panels.",
            GH_ParamAccess.list);
        pManager[photovoltaicPanels].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonEnergyModelParam(), "Model", "M", "InvisibleDragon energy model.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Valid", "V", "True when adjacency and model validation pass.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Adjacency and model diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "InvisibleDragon Model";
        var definitionGoos = new List<DragonZoneDefinitionGoo>();
        double northAxis = 0;
        string terrainText = "Suburbs";
        var photovoltaicGoos = new List<DragonPhotovoltaicPanelGoo>();
        if (!DA.GetData(0, ref name)
            || !DA.GetDataList(1, definitionGoos)
            || !DA.GetData(2, ref northAxis)
            || !DA.GetData(3, ref terrainText))
        {
            return;
        }

        DA.GetDataList(4, photovoltaicGoos);
        Terrain terrain = ChoiceInputs.ParseEnum<Terrain>(terrainText, "Terrain");

        InvisibleDragonZoneDefinition[] definitions = definitionGoos.Select((goo, index) => goo?.Value
            ?? throw new ArgumentException("Zones contains an empty value at position " + index + "."))
            .ToArray();
        AdjacencyResolution adjacency = InvisibleDragonAdjacencyResolver.Resolve(definitions);
        ZoneHvacAssignment[] hvacAssignments = definitions
            .Select((definition, index) => new { Definition = definition, Zone = adjacency.Zones[index] })
            .Where(item => item.Definition.SupplySystems.Count > 0)
            .Select(item => new ZoneHvacAssignment(
                item.Zone.Id,
                new SupplyGroup(item.Definition.SupplySystems)))
            .ToArray();
        ZoneVentilationAssignment[] ventilationAssignments = definitions
            .SelectMany((definition, index) => definition.Ventilators.Select(
                ventilator => new ZoneVentilationAssignment(adjacency.Zones[index].Id, ventilator)))
            .ToArray();
        PhotovoltaicPanel[] photovoltaicPanels = photovoltaicGoos.Select((goo, index) => goo?.Value
            ?? throw new ArgumentException("PV Panels contains an empty value at position " + index + "."))
            .ToArray();
        var model = new EnergyModel(
            name,
            adjacency.Zones,
            hvacAssignments,
            ventilationAssignments,
            photovoltaicPanels,
            northAxis,
            terrain);
        ValidationResult validation = model.Validate();
        Diagnostic[] diagnostics = adjacency.Diagnostics.Concat(validation.Diagnostics).ToArray();
        Report(diagnostics);
        DA.SetData(0, new DragonEnergyModelGoo(model));
        DA.SetData(1, diagnostics.All(item => !item.IsFailure));
        DA.SetDataList(2, diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}

internal sealed class AdjacencyResolution
{
    internal AdjacencyResolution(IReadOnlyList<Zone> zones, IReadOnlyList<Diagnostic> diagnostics)
    {
        Zones = zones;
        Diagnostics = diagnostics;
    }

    internal IReadOnlyList<Zone> Zones { get; }

    internal IReadOnlyList<Diagnostic> Diagnostics { get; }
}

internal static class InvisibleDragonAdjacencyResolver
{
    internal static AdjacencyResolution Resolve(IReadOnlyList<InvisibleDragonZoneDefinition> definitions)
    {
        if (definitions is null)
        {
            throw new ArgumentException("Zone definitions are required.", nameof(definitions));
        }

        Zone[] sourceZones = definitions.Select(item => item?.Zone
            ?? throw new ArgumentException("A Zone definition cannot be null.", nameof(definitions)))
            .ToArray();
        Surface[][] resolved = sourceZones.Select(zone => zone.Surfaces.ToArray()).ToArray();
        SurfaceReference[] surfaces = sourceZones
            .SelectMany((zone, zoneIndex) => zone.Surfaces.Select(
                (surface, surfaceIndex) => new SurfaceReference(zoneIndex, surfaceIndex, surface)))
            .ToArray();
        var candidates = surfaces.ToDictionary(item => item, _ => new List<SurfaceReference>());
        for (int firstIndex = 0; firstIndex < surfaces.Length; firstIndex++)
        {
            SurfaceReference first = surfaces[firstIndex];
            for (int secondIndex = firstIndex + 1; secondIndex < surfaces.Length; secondIndex++)
            {
                SurfaceReference second = surfaces[secondIndex];
                if (first.ZoneIndex != second.ZoneIndex
                    && CanBecomeAdjacent(first.Surface, second.Surface)
                    && first.Surface.Polygon.IsGeometricallyEquivalentTo(
                        second.Surface.Polygon,
                        allowReversedWinding: true,
                        GeometryTolerance.Distance))
                {
                    candidates[first].Add(second);
                    candidates[second].Add(first);
                }
            }
        }

        var diagnostics = new List<Diagnostic>();
        foreach (SurfaceReference surface in surfaces.Where(item => candidates[item].Count > 1))
        {
            string peers = string.Join(
                ", ",
                candidates[surface].Select(item => "'" + sourceZones[item.ZoneIndex].Name + "/" + item.Surface.Name + "'"));
            diagnostics.Add(new Diagnostic(
                "INVISIBLEDRAGON.GH.ADJACENCY_AMBIGUOUS",
                DiagnosticSeverity.Error,
                "Surface '" + sourceZones[surface.ZoneIndex].Name + "/" + surface.Surface.Name
                    + "' has multiple coincident candidates: " + peers + ".",
                surface.Surface.Id,
                surface.Surface.Provenance,
                "Remove coincident duplicate surfaces so exactly two surfaces from distinct Zones share each interzone boundary."));
        }

        var paired = new HashSet<SurfaceReference>();
        foreach (SurfaceReference first in surfaces)
        {
            if (paired.Contains(first) || candidates[first].Count != 1)
            {
                continue;
            }

            SurfaceReference second = candidates[first][0];
            if (paired.Contains(second) || candidates[second].Count != 1)
            {
                continue;
            }

            ValidationResult validation = SurfaceAdjacency.ValidateMatch(first.Surface, second.Surface);
            diagnostics.AddRange(validation.Diagnostics);
            if (!validation.IsValid)
            {
                paired.Add(first);
                paired.Add(second);
                continue;
            }

            SurfaceAdjacencyPair match = SurfaceAdjacency.Match(first.Surface, second.Surface);
            resolved[first.ZoneIndex][first.SurfaceIndex] = match.First;
            resolved[second.ZoneIndex][second.SurfaceIndex] = match.Second;
            paired.Add(first);
            paired.Add(second);
        }

        Zone[] zones = sourceZones.Select((zone, index) => new Zone(
            zone.Id,
            zone.Name,
            resolved[index],
            zone.Profile,
            zone.InfiltrationAirChangesPerHour,
            zone.LightingPowerDensityWattsPerSquareMetre,
            zone.OutdoorAirFlowCubicMetresPerSecond))
            .ToArray();
        return new AdjacencyResolution(zones, diagnostics);
    }

    private static bool CanBecomeAdjacent(Surface first, Surface second)
    {
        if (first.Boundary.Condition != SurfaceBoundaryCondition.Outdoors
            || second.Boundary.Condition != SurfaceBoundaryCondition.Outdoors)
        {
            return false;
        }

        return (first.Type, second.Type) switch
        {
            (SurfaceType.Wall, SurfaceType.Wall) => true,
            (SurfaceType.Floor, SurfaceType.Ceiling) => true,
            (SurfaceType.Ceiling, SurfaceType.Floor) => true,
            _ => false,
        };
    }

    private sealed class SurfaceReference
    {
        internal SurfaceReference(int zoneIndex, int surfaceIndex, Surface surface)
        {
            ZoneIndex = zoneIndex;
            SurfaceIndex = surfaceIndex;
            Surface = surface;
        }

        internal int ZoneIndex { get; }

        internal int SurfaceIndex { get; }

        internal Surface Surface { get; }
    }
}
