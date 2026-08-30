using System.Globalization;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Rhino;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using GonieGonie.SimpleDragon.Rhino;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Rhino;
using Rhino.Geometry;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

/// <summary>
/// Authors one geometry-backed opening. Ownership is expressed later by wiring
/// this value into exactly one SimpleDragon Zone component.
/// </summary>
public sealed class CreateSimpleDragonOpeningComponent : SimpleDragonComponent
{
    public CreateSimpleDragonOpeningComponent()
        : base(
            "SimpleDragon Opening",
            "SD Opening",
            "Creates a typed opening definition. Connect it directly to the Zone that owns it; no zone or face index is required.",
            SimpleDragonPanels.Geometry)
    {
    }

    public override Guid ComponentGuid => new("7d41fd2c-b93f-4fc8-88ea-db1f3abeb2f1");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddCurveParameter(
            "Boundary",
            "C",
            "Closed planar polygonal opening curve on its intended Zone face.",
            GH_ParamAccess.item);
        pManager.AddTextParameter("Name", "N", "Opening name.", GH_ParamAccess.item, "Opening");
        int type = pManager.AddIntegerParameter(
            "Type",
            "T",
            "Opening type.",
            GH_ParamAccess.item,
            (int)FenestrationType.Window);
        var typeParameter = (Param_Integer)pManager[type];
        foreach (FenestrationType value in Enum.GetValues(typeof(FenestrationType)))
        {
            typeParameter.AddNamedValue(value.ToString(), (int)value);
        }

        pManager.AddParameter(
            new SimpleDragonFenestrationConstructionParam(),
            "Construction",
            "FC",
            "Fenestration construction owned by this opening.",
            GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Blind",
            "Blind",
            "Optional Shade or Venetian; leave empty or use None for no blind.",
            GH_ParamAccess.item,
            "None");
        pManager.AddTextParameter(
            "ID",
            "ID",
            "Optional stable opening identifier.",
            GH_ParamAccess.item,
            string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new SimpleDragonOpeningDefinitionParam(),
            "Opening",
            "O",
            "Typed opening definition for one Zone.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new DiagnosticParam(),
            "Diagnostics",
            "D",
            "Opening authoring diagnostics.",
            GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        Curve? boundary = null;
        string name = "Opening";
        int typeValue = (int)FenestrationType.Window;
        SimpleDragonFenestrationConstructionGoo? constructionGoo = null;
        string blindText = "None";
        string id = string.Empty;
        if (!DA.GetData(0, ref boundary)
            || !DA.GetData(1, ref name)
            || !DA.GetData(2, ref typeValue)
            || !DA.GetData(3, ref constructionGoo))
        {
            return;
        }

        DA.GetData(4, ref blindText);
        DA.GetData(5, ref id);
        try
        {
            FenestrationType type = DefinedEnum<FenestrationType>(typeValue, "Type");
            BlindType? blind = OptionalEnum<BlindType>(blindText, "Blind");
            var opening = new OpeningDefinition(
                boundary!,
                name,
                type,
                constructionGoo?.Value
                    ?? throw new ArgumentException("Construction contains no value."),
                blind,
                OptionalId(id));
            DA.SetData(0, new SimpleDragonOpeningDefinitionGoo(opening));
            DA.SetDataList(1, Array.Empty<DiagnosticGoo>());
        }
        catch (Exception exception) when (IsAuthoringException(exception))
        {
            SetFailure(
                DA,
                1,
                "SD.GH.OPENING_INVALID",
                exception.Message,
                "Use a closed planar polygon, compatible construction/type, and valid optional ID.");
        }
    }

    private static T DefinedEnum<T>(int value, string inputName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), value))
        {
            throw new ArgumentOutOfRangeException(inputName, value, "Unknown " + inputName + " value.");
        }

        return (T)Enum.ToObject(typeof(T), value);
    }

    private static T? OptionalEnum<T>(string value, string inputName)
        where T : struct, Enum
    {
        string normalized = value.Trim();
        if (normalized.Length == 0 || string.Equals(normalized, "None", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Enum.TryParse(normalized, true, out T parsed)
            || !Enum.IsDefined(typeof(T), parsed))
        {
            throw new ArgumentException(
                inputName + " must be None, " + string.Join(", ", Enum.GetNames(typeof(T))) + ".");
        }

        return parsed;
    }

    internal static EntityId? OptionalId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : new EntityId(value.Trim());
    }

    internal static bool IsAuthoringException(Exception exception)
    {
        return exception is ArgumentException
            || exception is InvalidOperationException
            || exception is NotSupportedException;
    }

    internal void SetFailure(
        IGH_DataAccess access,
        int outputIndex,
        string code,
        string message,
        string suggestedAction)
    {
        var diagnostic = new Diagnostic(
            code,
            DiagnosticSeverity.Error,
            message,
            suggestedAction: suggestedAction);
        Report(new[] { diagnostic });
        access.SetDataList(outputIndex, new[] { new DiagnosticGoo(diagnostic) });
    }
}

/// <summary>
/// Collects all values owned by one thermal zone. Collective topology is resolved
/// by SimpleDragon Model so adjacent Breps are still evaluated together.
/// </summary>
public sealed class CreateSimpleDragonZoneComponent : SimpleDragonComponent
{
    public CreateSimpleDragonZoneComponent()
        : base(
            "SimpleDragon Zone",
            "SD Zone",
            "Collects one Zone Brep with its openings, HVAC, and usage values. The Model resolves all Zone definitions together so shared-face adjacency is preserved.",
            SimpleDragonPanels.Geometry)
    {
    }

    public override Guid ComponentGuid => new("79b35a81-b6a2-43cf-8f9d-361a655b63d1");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddBrepParameter("Zone Brep", "B", "One closed polygonal thermal-zone Brep.", GH_ParamAccess.item);
        pManager.AddTextParameter("Name", "N", "Zone name.", GH_ParamAccess.item, "Zone");
        pManager.AddIntegerParameter("Floor Number", "F", "Zone floor number.", GH_ParamAccess.item, 0);
        pManager.AddParameter(new SimpleDragonUsageProfileParam(), "Profile", "P", "Zone usage profile.", GH_ParamAccess.item);
        pManager.AddParameter(
            new SimpleDragonSurfaceConstructionParam(),
            "Surface Construction",
            "SC",
            "Optional construction owned by the faces extracted from this Zone Brep.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new SimpleDragonOpeningDefinitionParam(),
            "Openings",
            "O",
            "Completed openings owned by this Zone. Each owns its Construction; host faces are inferred geometrically.",
            GH_ParamAccess.list);
        pManager.AddParameter(
            new SimpleDragonSupplySystemParam(),
            "HVAC",
            "HVAC",
            "Supply systems owned by this Zone.",
            GH_ParamAccess.list);
        pManager.AddParameter(
            new SimpleDragonZoneErvParam(),
            "ERVs",
            "ERV",
            "ERV values owned by this Zone.",
            GH_ParamAccess.list);
        pManager.AddTextParameter(
            "Floor Boundary",
            "Floor BC",
            "Ground, Outdoors, or Adiabatic for unmatched floor faces.",
            GH_ParamAccess.item,
            "Ground");
        pManager.AddNumberParameter(
            "Lighting Power Density",
            "LPD",
            "Lighting power density in W/m².",
            GH_ParamAccess.item,
            10d);
        pManager[4].Optional = true;
        pManager[5].Optional = true;
        pManager[6].Optional = true;
        pManager[7].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new SimpleDragonZoneDefinitionParam(),
            "Zone",
            "Z",
            "Geometry-backed Zone definition for SimpleDragon Model.",
            GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Zone authoring diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        Brep? geometry = null;
        string name = "Zone";
        int floor = 0;
        SimpleDragonUsageProfileGoo? profileGoo = null;
        SimpleDragonSurfaceConstructionGoo? surfaceConstructionGoo = null;
        var openingGoos = new List<SimpleDragonOpeningDefinitionGoo>();
        var supplyGoos = new List<SimpleDragonSupplySystemGoo>();
        var ventilationGoos = new List<SimpleDragonZoneErvGoo>();
        string floorBoundaryText = "Ground";
        double lightDensity = 10d;
        if (!DA.GetData(0, ref geometry)
            || !DA.GetData(1, ref name)
            || !DA.GetData(2, ref floor)
            || !DA.GetData(3, ref profileGoo)
            || !DA.GetData(8, ref floorBoundaryText)
            || !DA.GetData(9, ref lightDensity))
        {
            return;
        }

        DA.GetData(4, ref surfaceConstructionGoo);
        DA.GetDataList(5, openingGoos);
        DA.GetDataList(6, supplyGoos);
        DA.GetDataList(7, ventilationGoos);
        try
        {
            UsageProfile profile = profileGoo?.Value
                ?? throw new ArgumentException("Profile contains no value.");
            if (!Enum.TryParse(floorBoundaryText.Trim(), true, out SurfaceBoundaryCondition floorBoundary)
                || (floorBoundary != SurfaceBoundaryCondition.Ground
                    && floorBoundary != SurfaceBoundaryCondition.Outdoors
                    && floorBoundary != SurfaceBoundaryCondition.Adiabatic))
            {
                throw new ArgumentException(
                    "Floor Boundary must be Ground, Outdoors, or Adiabatic.");
            }

            OpeningDefinition[] openings = openingGoos.Select((goo, index) => goo?.Value
                ?? throw new ArgumentException("Openings[" + index + "] contains no value."))
                .ToArray();
            SupplySystem[] supplies = supplyGoos.Select((goo, index) => goo?.Value
                ?? throw new ArgumentException("HVAC[" + index + "] contains no value."))
                .ToArray();
            VentilationAssignment[] ventilation = ventilationGoos.Select((goo, index) => goo?.Value
                ?? throw new ArgumentException("ERVs[" + index + "] contains no value."))
                .ToArray();
            var definition = new ZoneDefinition(
                geometry!,
                name,
                floor,
                profile,
                surfaceConstructionGoo?.Value,
                floorBoundary,
                lightDensity,
                openings,
                supplies,
                ventilation);
            DA.SetData(0, new SimpleDragonZoneDefinitionGoo(definition));
            DA.SetDataList(1, Array.Empty<DiagnosticGoo>());
        }
        catch (Exception exception) when (CreateSimpleDragonOpeningComponent.IsAuthoringException(exception))
        {
            var diagnostic = new Diagnostic(
                "SD.GH.ZONE_DEFINITION_INVALID",
                DiagnosticSeverity.Error,
                exception.Message,
                suggestedAction: "Connect one valid Brep, Profile, and only the openings and systems owned by this Zone.");
            Report(new[] { diagnostic });
            DA.SetDataList(1, new[] { new DiagnosticGoo(diagnostic) });
        }
    }
}

/// <summary>
/// Canonical collection boundary: all Zone definitions are resolved in one pass,
/// preserving adjacency while deriving every nested construction and HVAC catalog.
/// </summary>
public sealed class CreateSimpleDragonModelComponent : SimpleDragonComponent
{
    public CreateSimpleDragonModelComponent()
        : base(
            "SimpleDragon Model",
            "SD Model",
            "Resolves all Zone definitions together and creates a complete GRM. Nested openings, constructions, and HVAC are collected automatically.",
            SimpleDragonPanels.Model)
    {
    }

    public override Guid ComponentGuid => new("ce38124b-f99b-4d09-be3b-e5e5717db707");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Building/model name.", GH_ParamAccess.item, "SimpleDragon Model");
        pManager.AddParameter(
            new SimpleDragonZoneDefinitionParam(),
            "Zones",
            "Z",
            "Zone definitions. They are resolved collectively for shared-face adjacency.",
            GH_ParamAccess.list);
        pManager.AddNumberParameter("North Axis", "North", "Clockwise building north-axis rotation in degrees.", GH_ParamAccess.item, 0d);
        pManager.AddTextParameter(
            "Address",
            "A",
            "Korean address used internally to select climate metadata and packaged EPW.",
            GH_ParamAccess.item,
            "\uC11C\uC6B8\uD2B9\uBCC4\uC2DC \uC885\uB85C\uAD6C");
        pManager.AddTextParameter("Vintage", "V", "Building vintage as yyyy-MM-dd.", GH_ParamAccess.item, "2020-01-01");
        pManager.AddBooleanParameter("Multifamily Housing", "MF", "True for multifamily housing.", GH_ParamAccess.item, false);
        pManager.AddParameter(
            new SimpleDragonPhotovoltaicPanelParam(),
            "Photovoltaic Panels",
            "PV",
            "Optional model-level photovoltaic panels.",
            GH_ParamAccess.list);
        pManager[6].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitModelParam(), "GRM", "GRM", "Complete GRM 0.7 model.", GH_ParamAccess.item);
        pManager.AddParameter(new SimpleDragonZoneParam(), "Zones", "Z", "Resolved immutable thermal zones.", GH_ParamAccess.list);
        pManager.AddParameter(new SimpleDragonSurfaceParam(), "Surfaces", "S", "Resolved area-based surfaces.", GH_ParamAccess.list);
        pManager.AddTextParameter("Geometry Map", "Map", "Domain ID to Rhino source/face/loop mapping.", GH_ParamAccess.list);
        pManager.AddGenericParameter(
            "Geometry Map Data",
            "Map Data",
            "Structured Rhino-independent geometry mapping for CSV and downstream workflows.",
            GH_ParamAccess.list);
        pManager.AddTextParameter("JSON", "J", "Deterministic GRM 0.7 JSON.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Floor Area", "A", "Total floor area in m².", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Geometry, weather, and model diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "SimpleDragon Model";
        var definitionGoos = new List<SimpleDragonZoneDefinitionGoo>();
        double northAxis = 0d;
        string address = "\uC11C\uC6B8\uD2B9\uBCC4\uC2DC \uC885\uB85C\uAD6C";
        string vintageText = "2020-01-01";
        bool multifamily = false;
        var photovoltaicGoos = new List<SimpleDragonPhotovoltaicPanelGoo>();
        if (!DA.GetData(0, ref name)
            || !DA.GetDataList(1, definitionGoos)
            || !DA.GetData(2, ref northAxis)
            || !DA.GetData(3, ref address)
            || !DA.GetData(4, ref vintageText)
            || !DA.GetData(5, ref multifamily))
        {
            return;
        }

        DA.GetDataList(6, photovoltaicGoos);
        var diagnostics = new List<Diagnostic>();
        var disposableGeometry = new List<IDisposable>();
        try
        {
            if (!DateTime.TryParseExact(
                    vintageText.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime vintage))
            {
                throw new ArgumentException("Vintage must use yyyy-MM-dd.");
            }

            ZoneDefinition[] definitions = definitionGoos.Select((goo, index) => goo?.Value
                ?? throw new ArgumentException("Zones[" + index + "] contains no value."))
                .ToArray();
            if (definitions.Length == 0)
            {
                throw new ArgumentException("At least one Zone definition is required.");
            }

            RhinoDoc? document = RhinoDoc.ActiveDoc;
            if (document is null)
            {
                throw new InvalidOperationException("An active Rhino document is required for units and tolerances.");
            }

            RhinoGeometryContext context = RhinoGeometryContext.FromDocument(document);
            var zoneGeometry = new List<Brep>(definitions.Length);
            var openingSourcesByZone = new List<List<RhinoFenestrationSource>>(definitions.Length);
            for (int zoneIndex = 0; zoneIndex < definitions.Length; zoneIndex++)
            {
                ZoneDefinition definition = definitions[zoneIndex];
                Brep geometry = definition.Geometry;
                disposableGeometry.Add(geometry);
                var openingSources = new List<RhinoFenestrationSource>(definition.Openings.Count);
                for (int openingIndex = 0; openingIndex < definition.Openings.Count; openingIndex++)
                {
                    OpeningDefinition opening = definition.Openings[openingIndex];
                    Curve boundary = opening.Geometry;
                    disposableGeometry.Add(boundary);
                    OpeningHostResolution host = OpeningHostResolver.Resolve(
                        geometry,
                        boundary,
                        context,
                        opening.Id);
                    diagnostics.AddRange(host.Diagnostics);
                    if (!host.IsSuccess)
                    {
                        continue;
                    }

                    openingSources.Add(new RhinoFenestrationSource(
                        boundary,
                        host.FaceIndex!.Value,
                        opening.Name,
                        opening.Type,
                        opening.Construction,
                        opening.Blind,
                        opening.Id,
                        grasshopperIndex: openingIndex));
                }

                zoneGeometry.Add(geometry);
                openingSourcesByZone.Add(openingSources);
            }

            diagnostics.AddRange(InteriorOpeningOwnershipResolver.Reconcile(
                zoneGeometry,
                openingSourcesByZone,
                context));
            if (diagnostics.Any(item => item.IsFailure))
            {
                FinishDiagnostics(DA, diagnostics);
                return;
            }

            var sources = new List<RhinoZoneSource>(definitions.Length);
            for (int zoneIndex = 0; zoneIndex < definitions.Length; zoneIndex++)
            {
                ZoneDefinition definition = definitions[zoneIndex];
                sources.Add(new RhinoZoneSource(
                    zoneGeometry[zoneIndex],
                    definition.Name,
                    definition.FloorNumber,
                    definition.Profile,
                    definition.LightDensity,
                    grasshopperIndex: zoneIndex,
                    fenestrations: openingSourcesByZone[zoneIndex],
                    surfaceConstruction: definition.SurfaceConstruction,
                    unmatchedFloorBoundary: definition.UnmatchedFloorBoundary));
            }

            if (diagnostics.Any(item => item.IsFailure))
            {
                FinishDiagnostics(DA, diagnostics);
                return;
            }

            RhinoZoneExtractionResult extraction = RhinoZoneExtractor.Extract(
                sources,
                context,
                new RhinoZoneExtractionOptions());
            diagnostics.AddRange(extraction.Diagnostics);
            if (extraction.Zones.Count != definitions.Length
                || diagnostics.Any(item => item.IsFailure))
            {
                if (extraction.Zones.Count != definitions.Length
                    && diagnostics.All(item => item.Code != "SD.GH.ZONE_RESOLUTION_INCOMPLETE"))
                {
                    diagnostics.Add(new Diagnostic(
                        "SD.GH.ZONE_RESOLUTION_INCOMPLETE",
                        DiagnosticSeverity.Error,
                        "Not every Zone definition could be resolved.",
                        suggestedAction: "Review the geometry diagnostics and repair the failing Zone Brep or opening."));
                }

                FinishDiagnostics(DA, diagnostics);
                return;
            }

            Zone[] zones = extraction.Zones.Select((zone, index) => AttachSystems(zone, definitions[index])).ToArray();
            SurfaceConstruction[] surfaceConstructions = DistinctById(
                zones.SelectMany(zone => zone.Surfaces)
                    .Select(surface => surface.Construction)
                    .OfType<SurfaceConstruction>());
            Material[] materials = DistinctById(
                surfaceConstructions.SelectMany(item => item.Layers).Select(item => item.Material));
            FenestrationConstruction[] fenestrationConstructions = DistinctById(
                zones.SelectMany(zone => zone.Surfaces)
                    .SelectMany(surface => surface.Fenestrations)
                    .Select(item => item.Construction)
                    .OfType<FenestrationConstruction>());
            SupplySystem[] supplySystems = DistinctById(zones.SelectMany(zone => zone.SupplySystems));
            SourceSystem[] sourceSystems = DistinctById(
                supplySystems.Select(item => item.SourceSystem).OfType<SourceSystem>());
            VentilationSystem[] ventilationSystems = DistinctById(
                zones.SelectMany(zone => zone.VentilationAssignments)
                    .Select(item => item.VentilationSystem)
                    .OfType<VentilationSystem>());
            PhotovoltaicSystem[] photovoltaicSystems = DistinctById(
                photovoltaicGoos.Select((goo, index) => goo?.Value
                    ?? throw new ArgumentException(
                        "Photovoltaic Panels[" + index + "] contains no value.")));
            BuildingFloor[] floors = zones
                .GroupBy(zone => zone.FloorNumber)
                .OrderBy(group => group.Key)
                .Select(group => new BuildingFloor(group.Key, group))
                .ToArray();
            LookupResult<WeatherSelection> weather = SimpleDragonDatabase.Default.Weather.FindByAddress(
                address,
                vintage);
            diagnostics.AddRange(weather.Diagnostics);
            var model = new GreenRetrofitModel(
                name,
                northAxis,
                address,
                vintage,
                multifamily,
                floors,
                materials,
                surfaceConstructions,
                fenestrationConstructions,
                sourceSystems,
                supplySystems,
                ventilationSystems,
                photovoltaicSystems,
                weather.Value);
            diagnostics.Add(new Diagnostic(
                "SD.GH.AZIMUTH_USES_WORLD_NORTH",
                DiagnosticSeverity.Info,
                "Extracted wall azimuths use Rhino world north; the GRM North Axis remains a separate model value.",
                suggestedAction: "Do not pre-rotate Zone geometry to apply the model North Axis."));

            DA.SetData(0, new GreenRetrofitModelGoo(model));
            DA.SetDataList(1, zones.Select(item => new SimpleDragonZoneGoo(item)));
            DA.SetDataList(2, zones.SelectMany(item => item.Surfaces).Select(item => new SimpleDragonSurfaceGoo(item)));
            DA.SetDataList(3, extraction.GeometryMap.Select(FormatMap));
            DA.SetDataList(4, extraction.GeometryMap.Select(ToCoreGeometryMapEntry));
            DA.SetData(5, GrmWriter.Serialize(model));
            DA.SetData(6, model.Area);
            FinishDiagnostics(DA, diagnostics);
        }
        catch (Exception exception) when (CreateSimpleDragonOpeningComponent.IsAuthoringException(exception))
        {
            diagnostics.Add(new Diagnostic(
                "SD.GH.MODEL_COMPOSITION_INVALID",
                DiagnosticSeverity.Error,
                exception.Message,
                suggestedAction: "Review the connected Zone definitions and model metadata."));
            FinishDiagnostics(DA, diagnostics);
        }
        finally
        {
            foreach (IDisposable disposable in disposableGeometry)
            {
                disposable.Dispose();
            }
        }
    }

    private static Zone AttachSystems(Zone zone, ZoneDefinition definition)
    {
        return new Zone(
            zone.Name,
            zone.FloorNumber,
            zone.Height,
            zone.Surfaces,
            zone.ProfileName,
            zone.Profile,
            zone.LightDensity,
            definition.SupplySystems.Select(item => new SupplySystemAssignment(item.Id.Value, item)),
            definition.VentilationAssignments,
            zone.Id);
    }

    private void FinishDiagnostics(IGH_DataAccess access, IReadOnlyList<Diagnostic> diagnostics)
    {
        Report(diagnostics);
        access.SetDataList(7, diagnostics.Select(item => new DiagnosticGoo(item)));
    }

    private static T[] DistinctById<T>(IEnumerable<T> values)
        where T : class
    {
        return values
            .GroupBy(value => Id(value), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static string Id<T>(T value)
        where T : class
    {
        return value switch
        {
            Material item => item.Id.Value,
            SurfaceConstruction item => item.Id.Value,
            FenestrationConstruction item => item.Id.Value,
            SourceSystem item => item.Id.Value,
            SupplySystem item => item.Id.Value,
            VentilationSystem item => item.Id.Value,
            PhotovoltaicSystem item => item.Id.Value,
            _ => throw new ArgumentException("Unsupported catalog type '" + typeof(T).FullName + "'."),
        };
    }

    private static string FormatMap(RhinoDomainGeometryMapEntry entry)
    {
        string face = entry.FaceIndex?.ToString(CultureInfo.InvariantCulture) ?? "-";
        string brepLoop = entry.BrepLoopIndex?.ToString(CultureInfo.InvariantCulture) ?? "-";
        string openingSource = entry.FenestrationSourceIndex?.ToString(CultureInfo.InvariantCulture) ?? "-";
        return entry.EntityId.Value
            + " | " + entry.Kind
            + " | source " + entry.SourceIndex.ToString(CultureInfo.InvariantCulture)
            + " | face " + face
            + " | brep loop " + brepLoop
            + " | opening source " + openingSource
            + " | " + entry.Provenance.GeometryFingerprint;
    }

    private static GreenRetrofitGeometryMapEntry ToCoreGeometryMapEntry(RhinoDomainGeometryMapEntry entry)
    {
        GreenRetrofitGeometryKind kind = entry.Kind switch
        {
            RhinoMappedGeometryKind.Zone => GreenRetrofitGeometryKind.Zone,
            RhinoMappedGeometryKind.Surface => GreenRetrofitGeometryKind.Surface,
            RhinoMappedGeometryKind.Fenestration => GreenRetrofitGeometryKind.Fenestration,
            _ => throw new ArgumentOutOfRangeException(nameof(entry)),
        };
        return new GreenRetrofitGeometryMapEntry(
            entry.EntityId,
            kind,
            entry.SourceIndex,
            entry.FaceIndex,
            entry.BrepLoopIndex,
            entry.FenestrationSourceIndex,
            entry.Provenance);
    }
}
