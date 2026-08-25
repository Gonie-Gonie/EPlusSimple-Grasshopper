using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GonieGonie.Dragons.ExampleDefinitions;

internal static class AdvancedExampleDefinitions
{
    private const string InvisibleProduct = "InvisibleDragon";
    private const string SimpleProduct = "SimpleDragon";
    private const string TwoZoneModel = "30-two-zone-office.3dm";
    private static readonly int[] SingleBatchParallelLimit = { 1 };
    private static readonly string[] CsvLineSeparators = { "\r\n", "\n", "\r" };

    private static readonly AdvancedDefinition[] Definitions =
    {
        new(
            InvisibleProduct,
            "01-invisibledragon-envelope-profile.gh",
            BuildInvisibleEnvelope),
        new(
            InvisibleProduct,
            "02-invisibledragon-single-zone-hvac-idf.gh",
            BuildInvisibleSingleZone),
        new(
            SimpleProduct,
            "11-simpledragon-envelope-hvac.gh",
            BuildSimpleEnvelopeHvac),
        new(
            SimpleProduct,
            "12-simpledragon-two-zone-to-idf.gh",
            BuildSimpleTwoZone),
        new(
            SimpleProduct,
            "13-simpledragon-results-and-plots.gh",
            BuildSimpleResultsAndPlots),
        new(
            SimpleProduct,
            "14-simpledragon-two-zone-run-results-csv.gh",
            BuildSimpleRunResultsCsv),
    };

    internal static IReadOnlyList<Guid> ComponentIds(string product)
    {
        return Catalog.All
            .Where(item => string.Equals(item.Product, product, StringComparison.Ordinal))
            .Select(item => item.Id)
            .Distinct()
            .ToArray();
    }

    internal static IReadOnlyList<ExampleDefinitionResult> Run(ExampleHostInputs inputs)
    {
        GH_ComponentServer server = Instances.ComponentServer;
        return Definitions.Select(definition => inputs.Action == ExampleHostAction.Generate
            ? Generate(server, definition, inputs)
            : Validate(server, definition, inputs)).ToArray();
    }

    private static ExampleDefinitionResult Generate(
        GH_ComponentServer server,
        AdvancedDefinition definition,
        ExampleHostInputs inputs)
    {
        string candidatePath = StagedDefinitionPath(
            inputs.OutputDirectory,
            "generated",
            definition.FileName,
            inputs.ExamplesRoot);
        ScenarioGraph graph = definition.Build(server);
        Save(graph.Document, candidatePath);

        GH_Document candidate = Open(candidatePath);
        ValidationFacts candidateFacts = ValidateGraph(
            candidate,
            graph,
            inputs,
            exerciseRuntimeWorkflow: false);
        string canonicalPath = Path.Combine(inputs.ExamplesRoot, definition.FileName);
        File.Copy(candidatePath, canonicalPath, overwrite: true);
        GH_Document canonical = Open(canonicalPath);
        ValidationFacts canonicalFacts = ValidateGraph(
            canonical,
            graph,
            inputs,
            exerciseRuntimeWorkflow: true);
        Require(
            candidateFacts.ObjectCount == canonicalFacts.ObjectCount
                && candidateFacts.WireCount == canonicalFacts.WireCount,
            definition.FileName + " changed while publishing its generated candidate.");
        return Result(definition, canonicalPath, canonicalFacts, generated: true);
    }

    private static ExampleDefinitionResult Validate(
        GH_ComponentServer server,
        AdvancedDefinition definition,
        ExampleHostInputs inputs)
    {
        string canonicalPath = Path.Combine(inputs.ExamplesRoot, definition.FileName);
        if (!File.Exists(canonicalPath))
        {
            throw new FileNotFoundException(
                "Tracked advanced Grasshopper example is absent. Run with -Generate first.",
                canonicalPath);
        }

        ScenarioGraph graph = definition.Build(server);
        GH_Document canonical = Open(canonicalPath);
        ValidationFacts facts = ValidateGraph(
            canonical,
            graph,
            inputs,
            exerciseRuntimeWorkflow: false);
        string roundTripPath = StagedDefinitionPath(
            inputs.OutputDirectory,
            "roundtrip",
            definition.FileName,
            inputs.ExamplesRoot);
        Save(canonical, roundTripPath);
        GH_Document roundTrip = Open(roundTripPath);
        ValidationFacts reopened = ValidateGraph(
            roundTrip,
            graph,
            inputs,
            exerciseRuntimeWorkflow: true);
        Require(
            facts.ObjectCount == reopened.ObjectCount && facts.WireCount == reopened.WireCount,
            definition.FileName + " changed structure during its round trip.");
        return Result(definition, canonicalPath, reopened, generated: false);
    }

    private static ScenarioGraph BuildInvisibleEnvelope(GH_ComponentServer server)
    {
        var graph = new ScenarioGraphBuilder(server, "11000000");
        GraphNode concreteName = graph.Panel(1, "Exterior concrete", "Exterior Concrete", 60, 80);
        GraphNode insulationName = graph.Panel(2, "Mineral wool", "Mineral Wool", 60, 220);
        GraphNode finishName = graph.Panel(3, "Interior finish", "Interior Finish", 60, 360);
        GraphNode concrete = graph.Component(4, Catalog.InvisibleMaterial, 280, 60);
        GraphNode insulation = graph.Component(5, Catalog.InvisibleMaterial, 280, 200);
        GraphNode finish = graph.Component(6, Catalog.InvisibleMaterial, 280, 340);
        GraphNode concreteThickness = graph.Slider(7, "Concrete 0.200 m", 0.2m, 0.01m, 0.5m, 280, 480);
        GraphNode insulationThickness = graph.Slider(8, "Insulation 0.120 m", 0.12m, 0.01m, 0.5m, 280, 570);
        GraphNode finishThickness = graph.Slider(9, "Finish 0.013 m", 0.013m, 0.001m, 0.1m, 280, 660);
        GraphNode construction = graph.Component(10, Catalog.InvisibleConstruction, 650, 240);
        GraphNode noMass = graph.Component(11, Catalog.InvisibleNoMass, 650, 500);
        GraphNode profile = graph.Component(12, Catalog.InvisibleProfile, 650, 650);
        GraphNode uValue = graph.Panel(13, "Layered U-value", string.Empty, 980, 270);
        GraphNode layeredValue = graph.Panel(14, "Layered construction", string.Empty, 980, 370);
        GraphNode noMassValue = graph.Panel(15, "No-mass construction", string.Empty, 980, 520);
        GraphNode profileValue = graph.Panel(16, "Annual profile", string.Empty, 980, 670);

        graph.Connect(concreteName, null, concrete, 0);
        graph.Connect(insulationName, null, insulation, 0);
        graph.Connect(finishName, null, finish, 0);
        foreach (GraphNode material in new[] { concrete, insulation, finish })
        {
            graph.Connect(material, 0, construction, 1);
        }

        foreach (GraphNode thickness in new[] { concreteThickness, insulationThickness, finishThickness })
        {
            graph.Connect(thickness, null, construction, 2);
        }

        graph.Connect(construction, 1, uValue, null);
        graph.Connect(construction, 0, layeredValue, null);
        graph.Connect(noMass, 0, noMassValue, null);
        graph.Connect(profile, 0, profileValue, null);
        graph.ExpectOutput(construction, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonConstructionGoo");
        graph.ExpectOutput(construction, 1, 1);
        graph.ExpectOutput(noMass, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonConstructionGoo");
        graph.ExpectOutput(profile, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonProfileGoo");
        graph.ExpectOutput(uValue, null, 1);
        return graph.Build(
            InvisibleProduct,
            "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonConstructionGoo");
    }

    private static ScenarioGraph BuildInvisibleSingleZone(GH_ComponentServer server)
    {
        var graph = new ScenarioGraphBuilder(server, "12000000");
        GraphNode material = graph.Component(1, Catalog.InvisibleMaterial, 80, 80);
        GraphNode thickness = graph.Slider(2, "Envelope 0.200 m", 0.2m, 0.01m, 0.5m, 100, 230);
        GraphNode construction = graph.Component(3, Catalog.InvisibleConstruction, 360, 120);
        GraphNode profile = graph.Component(4, Catalog.InvisibleProfile, 360, 600);
        graph.Connect(material, 0, construction, 1);
        graph.Connect(thickness, null, construction, 2);

        Point3d[][] polygons =
        {
            new[] { new Point3d(0, 6, 0), new Point3d(8, 6, 0), new Point3d(8, 0, 0), new Point3d(0, 0, 0) },
            new[] { new Point3d(0, 0, 3), new Point3d(8, 0, 3), new Point3d(8, 6, 3), new Point3d(0, 6, 3) },
            new[] { new Point3d(0, 0, 0), new Point3d(8, 0, 0), new Point3d(8, 0, 3), new Point3d(0, 0, 3) },
            new[] { new Point3d(0, 6, 0), new Point3d(0, 6, 3), new Point3d(8, 6, 3), new Point3d(8, 6, 0) },
            new[] { new Point3d(0, 0, 0), new Point3d(0, 0, 3), new Point3d(0, 6, 3), new Point3d(0, 6, 0) },
            new[] { new Point3d(8, 0, 0), new Point3d(8, 6, 0), new Point3d(8, 6, 3), new Point3d(8, 0, 3) },
        };
        string[] names = { "Floor", "Roof", "South Wall", "North Wall", "West Wall", "East Wall" };
        GraphNode[] curves = new GraphNode[6];
        GraphNode[] surfaces = new GraphNode[6];
        GraphNode[] namePanels = new GraphNode[6];
        for (int index = 0; index < 6; index++)
        {
            curves[index] = graph.Curves(10 + index, names[index] + " boundary", new[] { ClosedCurve(polygons[index]) }, 80, 390 + (index * 120));
            surfaces[index] = graph.Component(20 + index, Catalog.InvisibleSurface, 650, 350 + (index * 130));
            namePanels[index] = graph.Panel(30 + index, names[index] + " name", names[index], 360, 350 + (index * 130));
            graph.Connect(curves[index], null, surfaces[index], 0);
            graph.Connect(namePanels[index], null, surfaces[index], 1);
            graph.Connect(construction, 0, surfaces[index], 3);
            graph.ExpectOutput(surfaces[index], 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonSurfaceGoo");
        }

        GraphNode floorType = graph.Panel(40, "Floor type", "Floor", 470, 980);
        GraphNode ceilingType = graph.Panel(41, "Ceiling type", "Ceiling", 470, 1070);
        GraphNode groundBoundary = graph.Panel(42, "Ground boundary", "Ground", 470, 1160);
        graph.Connect(floorType, null, surfaces[0], 2);
        graph.Connect(groundBoundary, null, surfaces[0], 4);
        graph.Connect(ceilingType, null, surfaces[1], 2);

        GraphNode zone = graph.Component(50, Catalog.InvisibleZone, 980, 560);
        foreach (GraphNode surface in surfaces)
        {
            graph.Connect(surface, 0, zone, 1);
        }

        graph.Connect(profile, 0, zone, 2);
        GraphNode heatPump = graph.Component(60, Catalog.InvisibleHeatPump, 80, 1280);
        GraphNode airHandler = graph.Component(61, Catalog.InvisibleAirHandler, 360, 1280);
        GraphNode boiler = graph.Component(62, Catalog.InvisibleBoiler, 80, 1480);
        GraphNode radiantFloor = graph.Component(63, Catalog.InvisibleRadiantFloor, 360, 1480);
        GraphNode ventilator = graph.Component(64, Catalog.InvisibleErv, 80, 1680);
        GraphNode photovoltaic = graph.Component(65, Catalog.InvisiblePv, 360, 1680);
        graph.Connect(heatPump, 0, airHandler, 1);
        graph.Connect(boiler, 0, radiantFloor, 1);

        GraphNode model = graph.Component(70, Catalog.InvisibleModel, 1320, 900);
        graph.Connect(zone, 0, model, 1);
        graph.Connect(heatPump, 0, model, 4);
        graph.Connect(boiler, 0, model, 4);
        graph.Connect(airHandler, 0, model, 5);
        graph.Connect(radiantFloor, 0, model, 5);
        graph.Connect(ventilator, 0, model, 8);
        graph.Connect(photovoltaic, 0, model, 10);
        GraphNode compile = graph.Component(71, Catalog.InvisibleCompile, 1650, 900);
        GraphNode validate = graph.Component(72, Catalog.InvisibleValidate, 1960, 1000);
        GraphNode idfText = graph.Panel(80, "Compiled IDF", string.Empty, 1960, 820);
        GraphNode valid = graph.Panel(81, "IDD validation", string.Empty, 2260, 1020);
        graph.Connect(model, 0, compile, 0);
        graph.Connect(compile, 0, validate, 0);
        graph.Connect(compile, 1, idfText, null);
        graph.Connect(validate, 0, valid, null);
        graph.ExpectOutput(zone, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonZoneGoo");
        graph.ExpectOutput(airHandler, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonSupplySystemGoo");
        graph.ExpectOutput(radiantFloor, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonSupplySystemGoo");
        graph.ExpectOutput(model, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonEnergyModelGoo");
        graph.ExpectOutput(compile, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonIdfGoo");
        graph.ExpectOutput(validate, 0, 1);
        graph.ExpectOutput(idfText, null, 1);
        graph.ExpectBoolean(zone, 1, true);
        graph.ExpectBoolean(model, 1, true);
        graph.ExpectBoolean(compile, 2, true);
        graph.ExpectBoolean(validate, 0, true);
        return graph.Build(
            InvisibleProduct,
            "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonIdfGoo",
            envelope: new OutwardEnvelopeExpectation(
                curves.Select(item => item.InstanceGuid).ToArray(),
                new Point3d(4, 3, 1.5)));
    }

    private static ScenarioGraph BuildSimpleEnvelopeHvac(GH_ComponentServer server)
    {
        var graph = new ScenarioGraphBuilder(server, "21000000");
        string[] materialNames = { "Exterior Concrete", "Mineral Wool", "Gypsum Board" };
        decimal[] values = { 0.2m, 0.12m, 0.013m };
        GraphNode[] materials = new GraphNode[3];
        GraphNode[] thicknesses = new GraphNode[3];
        for (int index = 0; index < 3; index++)
        {
            GraphNode name = graph.Panel(1 + index, materialNames[index] + " name", materialNames[index], 60, 80 + (index * 150));
            materials[index] = graph.Component(10 + index, Catalog.SimpleMaterial, 300, 60 + (index * 150));
            thicknesses[index] = graph.Slider(20 + index, materialNames[index] + " thickness", values[index], 0.001m, 0.5m, 300, 500 + (index * 90));
            graph.Connect(name, null, materials[index], 0);
        }

        GraphNode construction = graph.Component(30, Catalog.SimpleConstruction, 680, 230);
        foreach (GraphNode material in materials)
        {
            graph.Connect(material, 0, construction, 1);
        }

        foreach (GraphNode layerThickness in thicknesses)
        {
            graph.Connect(layerThickness, null, construction, 2);
        }

        GraphNode fenestration = graph.Component(31, Catalog.SimpleFenestration, 680, 520);
        GraphNode profileName = graph.Panel(32, "Packaged office profile", "\uC18C\uADDC\uBAA8\uC0AC\uBB34\uC2E4", 680, 730);
        GraphNode profile = graph.Component(33, Catalog.SimpleProfile, 980, 700);
        graph.Connect(profileName, null, profile, 0);
        GraphNode heatPump = graph.Component(40, Catalog.SimpleHeatPump, 60, 900);
        GraphNode airHandler = graph.Component(41, Catalog.SimpleAirHandler, 360, 900);
        GraphNode boiler = graph.Component(42, Catalog.SimpleBoiler, 60, 1090);
        GraphNode radiator = graph.Component(43, Catalog.SimpleRadiator, 360, 1090);
        GraphNode chiller = graph.Component(44, Catalog.SimpleChiller, 60, 1280);
        GraphNode fanCoil = graph.Component(45, Catalog.SimpleFanCoil, 360, 1280);
        GraphNode ventilator = graph.Component(46, Catalog.SimpleErv, 680, 970);
        GraphNode photovoltaic = graph.Component(47, Catalog.SimplePv, 680, 1180);
        graph.Connect(heatPump, 0, airHandler, 1);
        graph.Connect(boiler, 0, radiator, 1);
        graph.Connect(chiller, 0, fanCoil, 1);
        GraphNode uValue = graph.Panel(50, "Envelope U-value", string.Empty, 1040, 250);
        GraphNode profileValue = graph.Panel(51, "Resolved profile", string.Empty, 1280, 720);
        GraphNode systems = graph.Panel(52, "HVAC families", string.Empty, 1040, 1050);
        graph.Connect(construction, 1, uValue, null);
        graph.Connect(profile, 0, profileValue, null);
        graph.Connect(airHandler, 0, systems, null);
        graph.Connect(radiator, 0, systems, null);
        graph.Connect(fanCoil, 0, systems, null);
        graph.ExpectOutput(construction, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonSurfaceConstructionGoo");
        graph.ExpectOutput(fenestration, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonFenestrationConstructionGoo");
        graph.ExpectOutput(profile, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonUsageProfileGoo");
        graph.ExpectOutput(airHandler, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonSupplySystemGoo");
        graph.ExpectOutput(radiator, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonSupplySystemGoo");
        graph.ExpectOutput(fanCoil, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonSupplySystemGoo");
        graph.ExpectOutput(ventilator, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonEnergyRecoveryVentilatorGoo");
        graph.ExpectOutput(photovoltaic, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonPhotovoltaicPanelGoo");
        graph.ExpectOutput(systems, null, 3);
        return graph.Build(
            SimpleProduct,
            "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonSurfaceConstructionGoo");
    }

    private static ScenarioGraph BuildSimpleTwoZone(GH_ComponentServer server)
    {
        return BuildSimpleTwoZoneWorkflow(server, "22000000", includeRuntimeWorkflow: false);
    }

    private static ScenarioGraph BuildSimpleRunResultsCsv(GH_ComponentServer server)
    {
        return BuildSimpleTwoZoneWorkflow(server, "24000000", includeRuntimeWorkflow: true);
    }

    private static ScenarioGraph BuildSimpleTwoZoneWorkflow(
        GH_ComponentServer server,
        string instancePrefix,
        bool includeRuntimeWorkflow)
    {
        var graph = new ScenarioGraphBuilder(server, instancePrefix);
        Brep[] zoneGeometry = ExampleBuildingModels.CreateZoneBreps(TwoZoneModel);
        Curve[] openingGeometry = ExampleBuildingModels.CreateOpeningCurves(TwoZoneModel);
        GraphNode modelInfo = graph.Panel(
            1,
            "Rhino source model",
            "Open 30-two-zone-office.3dm; relink by object names when live references are preferred.",
            40,
            40);
        GraphNode breps = graph.Breps(2, "Two closed office zones", zoneGeometry, 60, 180);
        GraphNode openings = graph.Curves(3, "South facade windows", openingGeometry, 60, 360);
        GraphNode openingZoneIndices = graph.Integers(
            4,
            "Opening zone indices",
            ExampleBuildingModels.OpeningZoneIndices(TwoZoneModel),
            60,
            510);
        GraphNode openingFaceIndices = graph.Integers(
            5,
            "Opening face indices",
            ExampleBuildingModels.OpeningFaceIndices(TwoZoneModel),
            60,
            620);
        GraphNode profileName = graph.Panel(6, "Packaged office profile", "\uC18C\uADDC\uBAA8\uC0AC\uBB34\uC2E4", 330, 40);
        GraphNode profile = graph.Component(7, Catalog.SimpleProfile, 620, 40);
        GraphNode material = graph.Component(8, Catalog.SimpleMaterial, 330, 180);
        GraphNode thickness = graph.Slider(9, "Envelope 0.200 m", 0.2m, 0.01m, 0.5m, 350, 350);
        GraphNode construction = graph.Component(10, Catalog.SimpleConstruction, 620, 210);
        GraphNode fenestration = graph.Component(11, Catalog.SimpleFenestration, 620, 430);
        GraphNode extract = graph.Component(12, Catalog.SimpleExtractZones, 980, 260);
        graph.Connect(profileName, null, profile, 0);
        graph.Connect(material, 0, construction, 1);
        graph.Connect(thickness, null, construction, 2);
        graph.Connect(breps, null, extract, 0);
        graph.Connect(profile, 0, extract, 3);
        graph.Connect(construction, 0, extract, 4);
        graph.Connect(fenestration, 0, extract, 5);
        graph.Connect(openings, null, extract, 8);
        graph.Connect(openingZoneIndices, null, extract, 9);
        graph.Connect(openingFaceIndices, null, extract, 10);

        GraphNode heatPump = graph.Component(20, Catalog.SimpleHeatPump, 330, 780);
        GraphNode airHandler = graph.Component(21, Catalog.SimpleAirHandler, 620, 780);
        GraphNode boiler = graph.Component(22, Catalog.SimpleBoiler, 330, 970);
        GraphNode radiator = graph.Component(23, Catalog.SimpleRadiator, 620, 970);
        GraphNode assignSupplies = graph.Component(24, Catalog.SimpleAssignSupplies, 1320, 650);
        GraphNode ventilator = graph.Component(25, Catalog.SimpleErv, 980, 1050);
        GraphNode assignVentilation = graph.Component(26, Catalog.SimpleAssignVentilation, 1630, 720);
        GraphNode photovoltaic = graph.Component(27, Catalog.SimplePv, 1320, 1080);
        graph.Connect(heatPump, 0, airHandler, 1);
        graph.Connect(boiler, 0, radiator, 1);
        graph.Connect(extract, 0, assignSupplies, 0);
        graph.Connect(airHandler, 0, assignSupplies, 1);
        graph.Connect(radiator, 0, assignSupplies, 1);
        graph.Connect(assignSupplies, 0, assignVentilation, 0);
        graph.Connect(ventilator, 0, assignVentilation, 1);

        GraphNode assemble = graph.Component(30, Catalog.SimpleAssemble, 1960, 510);
        graph.Connect(assignVentilation, 0, assemble, 1);
        graph.Connect(material, 0, assemble, 6);
        graph.Connect(construction, 0, assemble, 7);
        graph.Connect(fenestration, 0, assemble, 8);
        graph.Connect(heatPump, 0, assemble, 9);
        graph.Connect(boiler, 0, assemble, 9);
        graph.Connect(airHandler, 0, assemble, 10);
        graph.Connect(radiator, 0, assemble, 10);
        graph.Connect(ventilator, 0, assemble, 11);
        graph.Connect(photovoltaic, 0, assemble, 12);
        GraphNode convert = graph.Component(31, Catalog.SimpleConvert, 2310, 540);
        graph.Connect(assemble, 0, convert, 0);
        GraphNode map = graph.Panel(40, "Geometry provenance map", string.Empty, 1320, 250);
        GraphNode area = graph.Panel(41, "Total floor area", string.Empty, 2310, 370);
        GraphNode idf = graph.Panel(42, "Converted IDF", string.Empty, 2640, 510);
        GraphNode success = graph.Panel(43, "Conversion success", string.Empty, 2640, 760);
        graph.Connect(extract, 2, map, null);
        graph.Connect(assemble, 2, area, null);
        graph.Connect(convert, 2, idf, null);
        graph.Connect(convert, 4, success, null);
        graph.ExpectOutput(extract, 0, 2, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonZoneGoo");
        graph.ExpectOutput(assignSupplies, 0, 2, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonZoneGoo");
        graph.ExpectOutput(assignVentilation, 0, 2, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonZoneGoo");
        graph.ExpectOutput(assemble, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.GreenRetrofitModelGoo");
        graph.ExpectOutput(convert, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonEnergyModelGoo");
        graph.ExpectOutput(convert, 1, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonIdfGoo");
        graph.ExpectOutput(convert, 2, 1);
        graph.ExpectOutput(map, null, 1);
        graph.ExpectOutput(area, null, 1);
        graph.ExpectOutput(success, null, 1);
        graph.ExpectNumber(assemble, 2, 96, 1e-8);
        graph.ExpectBoolean(convert, 4, true);

        RuntimeWorkflowExpectation? runtimeWorkflow = null;
        if (includeRuntimeWorkflow)
        {
            GraphNode runtimeRoot = graph.Panel(100, "EnergyPlus 24.2 root", string.Empty, 2770, 870);
            GraphNode weatherPath = graph.Panel(101, "EPW path", string.Empty, 2770, 970);
            GraphNode tempRoot = graph.Panel(
                102,
                "Run temp root",
                @"..\temp\example-preview\energyplus-run",
                2770,
                1070);
            GraphNode runTrigger = graph.Boolean(103, "Run - explicit rising edge", false, 2770, 1170);
            GraphNode cancelTrigger = graph.Boolean(104, "Cancel active run", false, 2770, 1250);
            GraphNode forceRerun = graph.Boolean(105, "Force rerun", false, 2770, 1330);
            GraphNode keepWork = graph.Boolean(106, "Keep work directory", true, 2770, 1410);
            GraphNode timeout = graph.Slider(107, "Run timeout 2 min", 2m, 1m, 30m, 2770, 1490);
            GraphNode prepareRuntime = graph.Boolean(108, "Prepare missing runtime", false, 2770, 1570);
            GraphNode run = graph.Component(110, Catalog.InvisibleRun, 3150, 1050);
            graph.Connect(convert, 1, run, 0);
            graph.Connect(weatherPath, null, run, 1);
            graph.Connect(runtimeRoot, null, run, 2);
            graph.Connect(tempRoot, null, run, 3);
            graph.Connect(runTrigger, null, run, 4);
            graph.Connect(cancelTrigger, null, run, 5);
            graph.Connect(forceRerun, null, run, 6);
            graph.Connect(keepWork, null, run, 7);
            graph.Connect(timeout, null, run, 8);
            graph.Connect(prepareRuntime, null, run, 9);

            GraphNode energyPlusSummary = graph.Component(111, Catalog.InvisibleResultSummary, 3540, 920);
            GraphNode buildResult = graph.Component(112, Catalog.SimpleBuildResult, 3540, 1180);
            GraphNode resultSummary = graph.Component(113, Catalog.SimpleResultSummary, 3900, 1160);
            GraphNode exportDirectory = graph.Panel(
                114,
                "CSV export directory",
                @"..\temp\example-preview\run-results-csv",
                3540,
                1510);
            GraphNode caseId = graph.Panel(115, "CSV case ID", "two-zone-office", 3540, 1600);
            GraphNode exportTrigger = graph.Boolean(116, "Export CSV", false, 3540, 1690);
            GraphNode overwrite = graph.Boolean(117, "Overwrite CSV", false, 3540, 1770);
            GraphNode exportCsv = graph.Component(118, Catalog.SimpleExportCsv, 3900, 1540);
            graph.Connect(run, 0, energyPlusSummary, 0);
            graph.Connect(assemble, 0, buildResult, 0);
            graph.Connect(run, 0, buildResult, 1);
            graph.Connect(buildResult, 0, resultSummary, 0);
            graph.Connect(buildResult, 0, exportCsv, 0);
            graph.Connect(assemble, 0, exportCsv, 1);
            graph.Connect(exportDirectory, null, exportCsv, 2);
            graph.Connect(caseId, null, exportCsv, 3);
            graph.Connect(run, 4, exportCsv, 4);
            graph.Connect(extract, 4, exportCsv, 5);
            graph.Connect(exportTrigger, null, exportCsv, 6);
            graph.Connect(overwrite, null, exportCsv, 7);

            GraphNode batchOutputRoot = graph.Panel(
                120,
                "Batch output root",
                @"..\temp\example-preview\simpledragon-batch",
                2770,
                1870);
            GraphNode parallelLimit = graph.Integers(121, "Batch parallel limit", SingleBatchParallelLimit, 2770, 1960);
            GraphNode batchRunTrigger = graph.Boolean(122, "Run batch - explicit rising edge", false, 2770, 2050);
            GraphNode batchCancelTrigger = graph.Boolean(123, "Cancel active batch", false, 2770, 2130);
            GraphNode batch = graph.Component(124, Catalog.SimpleBatch, 3150, 1900);
            graph.Connect(assemble, 0, batch, 0);
            graph.Connect(caseId, null, batch, 1);
            graph.Connect(weatherPath, null, batch, 2);
            graph.Connect(runtimeRoot, null, batch, 3);
            graph.Connect(batchOutputRoot, null, batch, 4);
            graph.Connect(parallelLimit, null, batch, 5);
            graph.Connect(batchRunTrigger, null, batch, 6);
            graph.Connect(batchCancelTrigger, null, batch, 7);

            GraphNode runState = graph.Panel(130, "EnergyPlus state", string.Empty, 3540, 700);
            GraphNode runSuccess = graph.Panel(131, "EnergyPlus success", string.Empty, 3900, 700);
            GraphNode summarySuccess = graph.Panel(132, "Parsed result success", string.Empty, 4280, 920);
            GraphNode grrSuccess = graph.Panel(133, "GRR build success", string.Empty, 3900, 1370);
            GraphNode annualResult = graph.Panel(134, "Annual site result", string.Empty, 4280, 1170);
            GraphNode csvFiles = graph.Panel(135, "CSV package files", string.Empty, 4280, 1540);
            GraphNode csvWritten = graph.Panel(136, "CSV written", string.Empty, 4280, 1740);
            GraphNode batchState = graph.Panel(137, "Batch state", string.Empty, 3540, 2050);
            GraphNode batchCsv = graph.Panel(138, "Batch combined CSV", string.Empty, 3900, 2000);
            GraphNode batchManifest = graph.Panel(139, "Batch manifest", string.Empty, 3900, 2130);
            GraphNode batchComplete = graph.Panel(140, "Batch complete", string.Empty, 4280, 2050);
            graph.Connect(run, 1, runState, null);
            graph.Connect(run, 2, runSuccess, null);
            graph.Connect(energyPlusSummary, 2, summarySuccess, null);
            graph.Connect(buildResult, 2, grrSuccess, null);
            graph.Connect(resultSummary, 1, annualResult, null);
            graph.Connect(exportCsv, 2, csvFiles, null);
            graph.Connect(exportCsv, 4, csvWritten, null);
            graph.Connect(batch, 0, batchState, null);
            graph.Connect(batch, 3, batchCsv, null);
            graph.Connect(batch, 4, batchManifest, null);
            graph.Connect(batch, 5, batchComplete, null);
            graph.ExpectOutput(run, 1, 1);
            graph.ExpectOutput(run, 2, 1);
            graph.ExpectOutput(runState, null, 1);
            graph.ExpectOutput(runSuccess, null, 1);
            graph.ExpectOutput(batch, 0, 1);
            graph.ExpectOutput(batch, 5, 1);
            graph.ExpectOutput(batchState, null, 1);
            graph.ExpectOutput(batchComplete, null, 1);
            graph.ExpectBoolean(run, 2, false);
            graph.ExpectBoolean(batch, 5, false);

            runtimeWorkflow = new RuntimeWorkflowExpectation(
                run.InstanceGuid,
                runTrigger.InstanceGuid,
                cancelTrigger.InstanceGuid,
                forceRerun.InstanceGuid,
                runtimeRoot.InstanceGuid,
                weatherPath.InstanceGuid,
                tempRoot.InstanceGuid,
                energyPlusSummary.InstanceGuid,
                buildResult.InstanceGuid,
                resultSummary.InstanceGuid,
                exportCsv.InstanceGuid,
                exportDirectory.InstanceGuid,
                exportTrigger.InstanceGuid,
                overwrite.InstanceGuid,
                batch.InstanceGuid,
                batchOutputRoot.InstanceGuid,
                batchRunTrigger.InstanceGuid,
                batchCancelTrigger.InstanceGuid);
        }

        return graph.Build(
            SimpleProduct,
            "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonIdfGoo",
            new LinkedModelExpectation(TwoZoneModel, breps.InstanceGuid, openings.InstanceGuid),
            runtimeWorkflow: runtimeWorkflow);
    }

    private static ScenarioGraph BuildSimpleResultsAndPlots(GH_ComponentServer server)
    {
        var graph = new ScenarioGraphBuilder(server, "23000000");
        GraphNode resultPath = graph.FilePath(
            1,
            "GRR fixture path",
            @"..\fixtures\simple-dragon\grr\ASHRAE 140 modified.grr",
            60,
            100);
        GraphNode read = graph.Component(2, Catalog.SimpleReadResult, 390, 90);
        GraphNode summary = graph.Component(3, Catalog.SimpleResultSummary, 760, 40);
        GraphNode dataTree = graph.Component(4, Catalog.SimpleDataTree, 760, 300);
        GraphNode linePlot = graph.Component(5, Catalog.SimpleLinePlot, 760, 560);
        GraphNode barPlot = graph.Component(6, Catalog.SimpleBarPlot, 760, 840);
        GraphNode export = graph.Component(7, Catalog.SimpleExportCsv, 760, 1160);
        GraphNode exportDirectory = graph.Panel(
            8,
            "Preview export directory",
            @"..\temp\example-preview\simpledragon-csv",
            390,
            1210);
        GraphNode annual = graph.Panel(10, "Annual site use", string.Empty, 1160, 70);
        GraphNode monthly = graph.Panel(11, "Monthly data tree", string.Empty, 1160, 330);
        GraphNode lines = graph.Panel(12, "Line plot curves", string.Empty, 1160, 590);
        GraphNode bars = graph.Panel(13, "Bar plot curves", string.Empty, 1160, 870);
        GraphNode csvFiles = graph.Panel(14, "CSV preview files", string.Empty, 1160, 1190);
        graph.Connect(resultPath, null, read, 0);
        foreach (GraphNode consumer in new[] { summary, dataTree, linePlot, barPlot, export })
        {
            graph.Connect(read, 0, consumer, 0);
        }

        graph.Connect(exportDirectory, null, export, 2);
        graph.Connect(summary, 1, annual, null);
        graph.Connect(dataTree, 3, monthly, null);
        graph.Connect(linePlot, 0, lines, null);
        graph.Connect(barPlot, 0, bars, null);
        graph.Connect(export, 1, csvFiles, null);
        graph.ExpectOutput(read, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.GreenRetrofitResultGoo");
        graph.ExpectOutput(summary, 2, 12);
        graph.ExpectOutput(dataTree, 3, 12);
        graph.ExpectOutput(linePlot, 0, 1);
        graph.ExpectOutput(barPlot, 0, 1);
        graph.ExpectOutput(export, 1, 1);
        graph.ExpectOutput(annual, null, 1);
        graph.ExpectOutput(monthly, null, 12);
        graph.ExpectOutput(lines, null, 1);
        graph.ExpectOutput(bars, null, 1);
        graph.ExpectOutput(csvFiles, null, 1);
        graph.ExpectBoolean(read, 2, true);
        graph.ExpectNumber(summary, 0, 48, 1e-8);
        graph.ExpectNumber(summary, 1, 79.34, 1e-8);
        graph.ExpectBoolean(export, 4, false);
        return graph.Build(
            SimpleProduct,
            "GonieGonie.SimpleDragon.Grasshopper.Types.GreenRetrofitResultGoo");
    }

    private static ValidationFacts ValidateGraph(
        GH_Document document,
        ScenarioGraph graph,
        ExampleHostInputs inputs,
        bool exerciseRuntimeWorkflow)
    {
        Require(
            document.ObjectCount == graph.Objects.Count,
            graph.Product + " advanced example must contain exactly " + graph.Objects.Count + " objects.");
        foreach (ObjectExpectation expected in graph.Objects)
        {
            IGH_DocumentObject actual = document.FindObject(expected.InstanceGuid, topLevelOnly: true)
                ?? throw new InvalidOperationException("Reopened definition lost object " + expected.InstanceGuid + ".");
            Require(
                string.Equals(actual.GetType().FullName, expected.TypeName, StringComparison.Ordinal),
                expected.InstanceGuid + " reopened as " + actual.GetType().FullName + " instead of " + expected.TypeName + ".");
            if (expected.ComponentGuid.HasValue)
            {
                Require(actual is GH_Component, expected.InstanceGuid + " must remain a Grasshopper component.");
                Require(
                    ((GH_Component)actual).ComponentGuid == expected.ComponentGuid.Value,
                    expected.InstanceGuid + " component identity changed.");
            }
        }

        int actualWireCount = CountWires(document);
        Require(
            actualWireCount == graph.Wires.Count,
            graph.Product + " advanced example has " + actualWireCount
                + " actual wires instead of " + graph.Wires.Count + ".");

        foreach (IGrouping<TargetKey, WireExpectation> targetGroup in graph.Wires.GroupBy(item => item.Target))
        {
            IGH_Param target = ResolveParam(document, targetGroup.Key.ObjectGuid, targetGroup.Key.Index, output: false);
            WireExpectation[] expectedSources = targetGroup.ToArray();
            Require(
                target.SourceCount == expectedSources.Length,
                "Wire target " + targetGroup.Key.ObjectGuid + " source count changed.");
            for (int index = 0; index < expectedSources.Length; index++)
            {
                WireExpectation expected = expectedSources[index];
                IGH_Param source = ResolveParam(document, expected.SourceObjectGuid, expected.SourceOutputIndex, output: true);
                Require(
                    target.Sources[index].InstanceGuid == source.InstanceGuid,
                    "Wire target " + targetGroup.Key.ObjectGuid + " source order or identity changed.");
            }
        }

        GH_Document.EnableSolutions = true;
        document.Enabled = true;
        document.NewSolution(true, GH_SolutionMode.Silent);
        foreach (GH_ActiveObject active in document.Objects.OfType<GH_ActiveObject>())
        {
            string[] errors = active.RuntimeMessages(GH_RuntimeMessageLevel.Error).ToArray();
            Require(
                errors.Length == 0,
                graph.Product + " object " + active.NickName + " reported errors: " + string.Join(" | ", errors));
        }

        foreach (OutputExpectation expected in graph.Outputs)
        {
            IGH_Param output = ResolveParam(document, expected.ObjectGuid, expected.OutputIndex, output: true);
            Require(
                output.VolatileData.DataCount >= expected.MinimumCount,
                expected.ObjectGuid + " produced " + output.VolatileData.DataCount + " values; expected at least " + expected.MinimumCount + ".");
            if (expected.GooType is not null)
            {
                object value = output.VolatileData.AllData(true).First();
                Require(
                    string.Equals(value.GetType().FullName, expected.GooType, StringComparison.Ordinal),
                    expected.ObjectGuid + " produced " + value.GetType().FullName + " instead of " + expected.GooType + ".");
            }
        }

        foreach (BooleanExpectation expected in graph.Booleans)
        {
            object value = ResolveParam(document, expected.ObjectGuid, expected.OutputIndex, output: true)
                .VolatileData
                .AllData(true)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(expected.ObjectGuid + " produced no Boolean value.");
            Require(value is GH_Boolean, expected.ObjectGuid + " did not produce a Grasshopper Boolean.");
            Require(
                ((GH_Boolean)value).Value == expected.Expected,
                expected.ObjectGuid + " produced " + ((GH_Boolean)value).Value
                    + " instead of " + expected.Expected + ".");
        }

        foreach (NumberExpectation expected in graph.Numbers)
        {
            object value = ResolveParam(document, expected.ObjectGuid, expected.OutputIndex, output: true)
                .VolatileData
                .AllData(true)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(expected.ObjectGuid + " produced no numeric value.");
            Require(value is GH_Number, expected.ObjectGuid + " did not produce a Grasshopper number.");
            double actual = ((GH_Number)value).Value;
            Require(
                Math.Abs(actual - expected.Expected) <= expected.Tolerance,
                expected.ObjectGuid + " produced " + actual.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " instead of " + expected.Expected.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        if (graph.Envelope is not null)
        {
            ValidateOutwardEnvelope(document, graph.Envelope);
        }

        if (graph.LinkedModel is not null)
        {
            Param_Brep brepParam = RequireObject<Param_Brep>(document, graph.LinkedModel.BrepParameterGuid);
            Param_Curve curveParam = RequireObject<Param_Curve>(document, graph.LinkedModel.CurveParameterGuid);
            Brep[] breps = brepParam.VolatileData.AllData(true)
                .OfType<GH_Brep>()
                .Select(item => item.Value)
                .Where(item => item is not null)
                .Cast<Brep>()
                .ToArray();
            Curve[] curves = curveParam.VolatileData.AllData(true)
                .OfType<GH_Curve>()
                .Select(item => item.Value)
                .Where(item => item is not null)
                .Cast<Curve>()
                .ToArray();
            ExampleBuildingModels.ValidateEmbeddedGeometry(
                graph.LinkedModel.FileName,
                inputs.ExamplesRoot,
                breps,
                curves);
        }

        RuntimeValidationFacts runtime = ValidateRuntimeWorkflow(
            document,
            graph.RuntimeWorkflow,
            inputs,
            exerciseRuntimeWorkflow);
        return new ValidationFacts(
            graph.Objects.Count,
            actualWireCount,
            graph.PrimaryOutputGooType,
            runtime);
    }

    private static RuntimeValidationFacts ValidateRuntimeWorkflow(
        GH_Document document,
        RuntimeWorkflowExpectation? expectation,
        ExampleHostInputs inputs,
        bool exercise)
    {
        if (expectation is null)
        {
            return NotExecutedRuntime(
                "not-applicable",
                "This definition has no executable EnergyPlus workflow.");
        }

        RequirePersistentBoolean(document, expectation.RunTriggerGuid, false);
        RequirePersistentBoolean(document, expectation.CancelTriggerGuid, false);
        RequirePersistentBoolean(document, expectation.ForceRerunGuid, false);
        RequirePersistentBoolean(document, expectation.ExportTriggerGuid, false);
        RequirePersistentBoolean(document, expectation.OverwriteGuid, false);
        RequirePersistentBoolean(document, expectation.BatchRunTriggerGuid, false);
        RequirePersistentBoolean(document, expectation.BatchCancelTriggerGuid, false);

        if (!exercise)
        {
            return NotExecutedRuntime(
                "deferred",
                "The saved workflow and safe False triggers were validated; runtime exercise is deferred to the final reopened document.");
        }

        if (!inputs.CanRunEnergyPlusWorkflow)
        {
            return NotExecutedRuntime(
                inputs.EnergyPlusGateStatus,
                inputs.EnergyPlusGateReason);
        }

        string runtimeRoot = inputs.EnergyPlusRuntimeRoot
            ?? throw new InvalidOperationException("The ready EnergyPlus gate has no runtime root.");
        string weatherPath = inputs.EnergyPlusWeatherPath
            ?? throw new InvalidOperationException("The ready EnergyPlus gate has no weather path.");
        string workflowRoot = Path.Combine(inputs.OutputDirectory, "runtime-workflow");
        string tempRoot = Path.Combine(workflowRoot, "energyplus-temp");
        string csvRoot = Path.Combine(workflowRoot, "csv-package");
        // Keep the batch roots beneath the host output while staying below the legacy
        // MAX_PATH limit exercised by Rhino 7 and the batch runner's hashed run paths.
        string batchRoot = Path.Combine(inputs.OutputDirectory, "b");
        string batchCancellationRoot = Path.Combine(inputs.OutputDirectory, "c");
        Require(!Directory.Exists(workflowRoot), "The runtime evidence directory already exists: " + workflowRoot);
        Directory.CreateDirectory(workflowRoot);
        DateTime evidenceNotBeforeUtc = DateTime.UtcNow.AddSeconds(-2);

        try
        {
            SetPanel(document, expectation.RuntimeRootGuid, runtimeRoot);
            SetPanel(document, expectation.WeatherPathGuid, weatherPath);
            SetPanel(document, expectation.TempRootGuid, tempRoot);
            SetPanel(document, expectation.ExportDirectoryGuid, csvRoot);
            SetPanel(document, expectation.BatchOutputRootGuid, batchRoot);
            SetBoolean(document, expectation.RunTriggerGuid, false);
            SetBoolean(document, expectation.CancelTriggerGuid, false);
            SetBoolean(document, expectation.ForceRerunGuid, false);
            SetBoolean(document, expectation.ExportTriggerGuid, false);
            SetBoolean(document, expectation.OverwriteGuid, false);
            SetBoolean(document, expectation.BatchRunTriggerGuid, false);
            SetBoolean(document, expectation.BatchCancelTriggerGuid, false);
            Solve(document);

            SetBoolean(document, expectation.RunTriggerGuid, true);
            Solve(document);
            string firstRunState = WaitForTerminalState(
                document,
                expectation.RunComponentGuid,
                "syncRoot",
                "activeTask",
                "stateText",
                inputs.EnergyPlusWorkflowTimeout,
                "Succeeded",
                "Failed",
                "Cancelled");
            SetBoolean(document, expectation.RunTriggerGuid, false);
            Solve(document);
            Require(
                string.Equals(firstRunState, "Succeeded", StringComparison.Ordinal),
                "The real EnergyPlus example run ended in " + firstRunState + ". "
                    + RuntimeMessages(document, expectation.RunComponentGuid));
            Require(ReadBoolean(document, expectation.RunComponentGuid, 2), "The EnergyPlus run did not report success.");
            RequireOutputType(
                document,
                expectation.RunComponentGuid,
                0,
                "GonieGonie.InvisibleDragon.Grasshopper.Types.EnergyPlusResultGoo");
            string workDirectory = ReadString(document, expectation.RunComponentGuid, 3);
            RequireContainedDirectory(workDirectory, tempRoot, "EnergyPlus work directory");
            Require(
                ReadBoolean(document, expectation.EnergyPlusSummaryGuid, 2),
                "EnergyPlus Result Summary did not confirm the successful runtime result.");
            Require(
                ReadInteger(document, expectation.EnergyPlusSummaryGuid, 4) == 0
                    && ReadInteger(document, expectation.EnergyPlusSummaryGuid, 5) == 0,
                "The successful EnergyPlus result exposed severe or fatal errors.");
            RequireOutputType(
                document,
                expectation.BuildResultGuid,
                0,
                "GonieGonie.SimpleDragon.Grasshopper.Types.GreenRetrofitResultGoo");
            Require(
                ReadBoolean(document, expectation.BuildResultGuid, 2),
                "Build SimpleDragon GRR did not convert the real monthly EnergyPlus tables.");
            double totalArea = ReadNumber(document, expectation.ResultSummaryGuid, 0);
            double annualResult = ReadNumber(document, expectation.ResultSummaryGuid, 1);
            double[] monthlyResults = ReadNumbers(document, expectation.ResultSummaryGuid, 2);
            Require(IsFinite(totalArea) && totalArea > 0, "SimpleDragon GRR Summary emitted an invalid floor area.");
            Require(IsFinite(annualResult), "SimpleDragon GRR Summary emitted a non-finite annual result.");
            Require(
                monthlyResults.Length == 12 && monthlyResults.All(IsFinite),
                "SimpleDragon GRR Summary did not emit twelve finite monthly results.");
            double monthlySum = monthlyResults.Sum();
            double annualTolerance = Math.Max(1e-6, Math.Abs(annualResult) * 1e-9);
            Require(
                Math.Abs(monthlySum - annualResult) <= annualTolerance,
                "SimpleDragon GRR annual result did not equal the twelve monthly values.");

            SetBoolean(document, expectation.OverwriteGuid, true);
            SetBoolean(document, expectation.ExportTriggerGuid, true);
            Solve(document);
            Require(
                ReadBoolean(document, expectation.ExportCsvGuid, 4),
                "Export SimpleDragon CSV did not report a completed write.");
            string[] csvNames = ReadStrings(document, expectation.ExportCsvGuid, 1);
            string[] csvFiles = ReadStrings(document, expectation.ExportCsvGuid, 2);
            string[] csvContents = ReadStrings(document, expectation.ExportCsvGuid, 3);
            Require(csvFiles.Length >= 4, "The CSV package exposed fewer than four output files.");
            Require(
                csvNames.Length == csvFiles.Length && csvContents.Length == csvFiles.Length,
                "The CSV package names, paths, and contents have different lengths.");
            Require(
                csvNames.Distinct(StringComparer.Ordinal).Count() == csvNames.Length,
                "The CSV package contains duplicate file names.");
            var csvHashes = new string[csvFiles.Length];
            for (int index = 0; index < csvFiles.Length; index++)
            {
                string fullPath = RequireFreshContainedFile(
                    csvFiles[index],
                    csvRoot,
                    evidenceNotBeforeUtc,
                    "CSV package file");
                Require(
                    string.Equals(Path.GetFileName(fullPath), csvNames[index], StringComparison.Ordinal),
                    "The CSV package file name does not match its reported path: " + csvNames[index] + ".");
                string writtenContent = File.ReadAllText(fullPath);
                Require(
                    string.Equals(writtenContent, csvContents[index], StringComparison.Ordinal),
                    "The CSV package file differs from the component's deterministic content: " + fullPath + ".");
                if (string.Equals(Path.GetExtension(fullPath), ".csv", StringComparison.OrdinalIgnoreCase))
                {
                    Require(writtenContent.Any(character => character == '\n'), "A CSV package file has no data rows: " + fullPath + ".");
                }

                csvHashes[index] = csvNames[index] + "=" + ComputeSha256(fullPath);
            }

            Require(
                csvContents.Any(content => content.Contains("goniegonie-simpledragon-csv-export.v1")
                    && content.Contains("two-zone-office")),
                "The CSV package manifest does not identify its schema and case.");
            int summaryCsvIndex = Array.FindIndex(
                csvNames,
                name => string.Equals(name, "summary.csv", StringComparison.Ordinal));
            Require(summaryCsvIndex >= 0, "The CSV package contains no summary.csv file.");
            RequireSummaryCsvMatchesResult(
                csvContents[summaryCsvIndex],
                totalArea,
                annualResult);
            SetBoolean(document, expectation.ExportTriggerGuid, false);
            SetBoolean(document, expectation.OverwriteGuid, false);
            Solve(document);

            SetBoolean(document, expectation.RunTriggerGuid, false);
            Solve(document);
            SetBoolean(document, expectation.RunTriggerGuid, true);
            Solve(document);
            string cachedRunState = WaitForTerminalState(
                document,
                expectation.RunComponentGuid,
                "syncRoot",
                "activeTask",
                "stateText",
                inputs.EnergyPlusWorkflowTimeout,
                "Cached",
                "Succeeded",
                "Failed",
                "Cancelled");
            SetBoolean(document, expectation.RunTriggerGuid, false);
            Solve(document);
            Require(
                string.Equals(cachedRunState, "Cached", StringComparison.Ordinal),
                "An identical EnergyPlus rerun did not use the component cache; state was " + cachedRunState + ".");
            Require(ReadBoolean(document, expectation.RunComponentGuid, 2), "The cached EnergyPlus result lost its success state.");
            RequireOutputType(
                document,
                expectation.RunComponentGuid,
                0,
                "GonieGonie.InvisibleDragon.Grasshopper.Types.EnergyPlusResultGoo");

            SetBoolean(document, expectation.ForceRerunGuid, true);
            Solve(document);
            SetBoolean(document, expectation.RunTriggerGuid, true);
            Solve(document);
            SetBoolean(document, expectation.CancelTriggerGuid, true);
            Solve(document);
            string cancellationState = WaitForTerminalState(
                document,
                expectation.RunComponentGuid,
                "syncRoot",
                "activeTask",
                "stateText",
                inputs.EnergyPlusWorkflowTimeout,
                "Cancelled",
                "Succeeded",
                "Failed");
            Require(
                string.Equals(cancellationState, "Cancelled", StringComparison.Ordinal),
                "The explicit EnergyPlus cancellation exercise ended in " + cancellationState + ".");
            Solve(document);
            Require(
                !ReadBoolean(document, expectation.RunComponentGuid, 2),
                "A cancelled EnergyPlus run incorrectly reported success.");
            SetBoolean(document, expectation.RunTriggerGuid, false);
            SetBoolean(document, expectation.CancelTriggerGuid, false);
            SetBoolean(document, expectation.ForceRerunGuid, false);
            Solve(document);

            SetBoolean(document, expectation.BatchRunTriggerGuid, true);
            Solve(document);
            string firstBatchState = WaitForTerminalState(
                document,
                expectation.BatchComponentGuid,
                "_syncRoot",
                "_activeTask",
                "_state",
                inputs.EnergyPlusWorkflowTimeout,
                "Succeeded",
                "Failed",
                "Cancelled",
                "Completed With Failures");
            SetBoolean(document, expectation.BatchRunTriggerGuid, false);
            Solve(document);
            Require(
                string.Equals(firstBatchState, "Succeeded", StringComparison.Ordinal),
                "The real SimpleDragon batch ended in " + firstBatchState + ". "
                    + RuntimeMessages(document, expectation.BatchComponentGuid));
            Require(ReadBoolean(document, expectation.BatchComponentGuid, 5), "The SimpleDragon batch was not complete.");
            RequireSingleBatchCase(document, expectation.BatchComponentGuid, "Succeeded");
            string combinedCsv = RequireFreshContainedFile(
                ReadString(document, expectation.BatchComponentGuid, 3),
                batchRoot,
                evidenceNotBeforeUtc,
                "SimpleDragon batch combined CSV");
            string manifest = RequireFreshContainedFile(
                ReadString(document, expectation.BatchComponentGuid, 4),
                batchRoot,
                evidenceNotBeforeUtc,
                "SimpleDragon batch manifest");
            string combinedCsvContent = File.ReadAllText(combinedCsv);
            string manifestContent = File.ReadAllText(manifest);
            Require(
                combinedCsvContent.StartsWith("index,case_id,status", StringComparison.Ordinal)
                    && combinedCsvContent.Contains("two-zone-office,Succeeded"),
                "The SimpleDragon batch combined CSV does not contain the successful ordered case.");
            RequireBatchCsvMatchesResult(combinedCsvContent, totalArea, annualResult);
            Require(
                manifestContent.Contains("goniegonie.simple-dragon.batch-manifest.v1")
                    && manifestContent.Contains("two-zone-office")
                    && manifestContent.Contains("\"status\": \"Succeeded\""),
                "The SimpleDragon batch manifest does not contain its schema and successful case.");

            SetBoolean(document, expectation.BatchRunTriggerGuid, true);
            Solve(document);
            string cachedBatchState = WaitForTerminalState(
                document,
                expectation.BatchComponentGuid,
                "_syncRoot",
                "_activeTask",
                "_state",
                inputs.EnergyPlusWorkflowTimeout,
                "Succeeded",
                "Failed",
                "Cancelled",
                "Completed With Failures");
            SetBoolean(document, expectation.BatchRunTriggerGuid, false);
            Solve(document);
            Require(
                string.Equals(cachedBatchState, "Succeeded", StringComparison.Ordinal),
                "The cached SimpleDragon batch ended in " + cachedBatchState + ".");
            Require(
                ReadBatchCacheHits(document, expectation.BatchComponentGuid) >= 1,
                "The identical SimpleDragon batch rerun did not report a cache hit.");
            RequireSingleBatchCase(document, expectation.BatchComponentGuid, "Succeeded");

            SetPanel(document, expectation.BatchOutputRootGuid, batchCancellationRoot);
            SetBoolean(document, expectation.BatchRunTriggerGuid, false);
            SetBoolean(document, expectation.BatchCancelTriggerGuid, false);
            Solve(document);
            SetBoolean(document, expectation.BatchRunTriggerGuid, true);
            Solve(document);
            string batchStartState = WaitForState(
                document,
                expectation.BatchComponentGuid,
                "_syncRoot",
                "_state",
                inputs.EnergyPlusWorkflowTimeout,
                "Running",
                "Succeeded",
                "Failed",
                "Cancelled",
                "Completed With Failures");
            Require(
                string.Equals(batchStartState, "Running", StringComparison.Ordinal),
                "The batch cancellation exercise reached " + batchStartState + " before cancellation could be requested.");
            SetBoolean(document, expectation.BatchCancelTriggerGuid, true);
            Solve(document);
            string batchCancellationState = WaitForTerminalState(
                document,
                expectation.BatchComponentGuid,
                "_syncRoot",
                "_activeTask",
                "_state",
                inputs.EnergyPlusWorkflowTimeout,
                "Cancelled",
                "Succeeded",
                "Failed",
                "Completed With Failures");
            SetBoolean(document, expectation.BatchRunTriggerGuid, false);
            SetBoolean(document, expectation.BatchCancelTriggerGuid, false);
            Solve(document);
            Require(
                string.Equals(batchCancellationState, "Cancelled", StringComparison.Ordinal),
                "The explicit SimpleDragon batch cancellation ended in " + batchCancellationState + ".");
            Require(
                !ReadBoolean(document, expectation.BatchComponentGuid, 5),
                "A cancelled SimpleDragon batch incorrectly reported complete success.");
            RequireSingleBatchCase(document, expectation.BatchComponentGuid, "Cancelled");
            string cancelledBatchCsv = RequireFreshContainedFile(
                ReadString(document, expectation.BatchComponentGuid, 3),
                batchCancellationRoot,
                evidenceNotBeforeUtc,
                "Cancelled SimpleDragon batch combined CSV");
            string cancelledBatchManifest = RequireFreshContainedFile(
                ReadString(document, expectation.BatchComponentGuid, 4),
                batchCancellationRoot,
                evidenceNotBeforeUtc,
                "Cancelled SimpleDragon batch manifest");
            Require(
                File.ReadAllText(cancelledBatchCsv).Contains("two-zone-office,Cancelled"),
                "The cancelled batch CSV does not preserve the cancelled case status.");
            Require(
                File.ReadAllText(cancelledBatchManifest).Contains("\"status\": \"Cancelled\""),
                "The cancelled batch manifest does not preserve the cancelled case status.");

            string finalRunState = ReadRuntimeSnapshot(
                document,
                expectation.RunComponentGuid,
                "syncRoot",
                "activeTask",
                "stateText").State;
            return new RuntimeValidationFacts(
                "ready",
                inputs.EnergyPlusGateReason,
                true,
                finalRunState,
                firstRunState,
                cachedRunState,
                cancellationState,
                firstBatchState,
                cachedBatchState,
                batchCancellationState,
                true,
                true,
                true,
                true,
                true,
                true,
                Path.GetFullPath(inputs.OutputDirectory),
                annualResult,
                csvHashes,
                ComputeSha256(combinedCsv),
                ComputeSha256(manifest),
                ComputeSha256(cancelledBatchCsv),
                ComputeSha256(cancelledBatchManifest));
        }
        finally
        {
            BestEffortCancelAndDrain(document, expectation, inputs.EnergyPlusWorkflowTimeout);
        }
    }

    private static RuntimeValidationFacts NotExecutedRuntime(string gateStatus, string gateReason)
    {
        return new RuntimeValidationFacts(
            gateStatus,
            gateReason,
            false,
            "Not Run",
            "Not Run",
            "Not Run",
            "Not Run",
            "Not Run",
            "Not Run",
            "Not Run",
            false,
            false,
            false,
            false,
            false,
            false,
            string.Empty,
            null,
            Array.Empty<string>(),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private static string WaitForTerminalState(
        GH_Document document,
        Guid componentGuid,
        string syncRootField,
        string activeTaskField,
        string stateField,
        TimeSpan timeout,
        params string[] terminalPrefixes)
    {
        var stopwatch = Stopwatch.StartNew();
        RuntimeComponentSnapshot snapshot = ReadRuntimeSnapshot(
            document,
            componentGuid,
            syncRootField,
            activeTaskField,
            stateField);
        while (stopwatch.Elapsed < timeout)
        {
            snapshot = ReadRuntimeSnapshot(
                document,
                componentGuid,
                syncRootField,
                activeTaskField,
                stateField);
            bool terminal = terminalPrefixes.Any(prefix => snapshot.State.StartsWith(prefix, StringComparison.Ordinal));
            if (terminal && !snapshot.HasActiveTask)
            {
                return snapshot.State;
            }

            System.Threading.Thread.Sleep(25);
        }

        throw new TimeoutException(
            componentGuid + " did not reach a terminal state within "
                + timeout.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " seconds. Last state: " + snapshot.State + ". " + RuntimeMessages(document, componentGuid));
    }

    private static string WaitForState(
        GH_Document document,
        Guid componentGuid,
        string syncRootField,
        string stateField,
        TimeSpan timeout,
        params string[] expectedPrefixes)
    {
        var stopwatch = Stopwatch.StartNew();
        string state = string.Empty;
        while (stopwatch.Elapsed < timeout)
        {
            GH_Component component = RequireObject<GH_Component>(document, componentGuid);
            FieldInfo syncField = RequirePrivateField(component, syncRootField);
            FieldInfo stateValueField = RequirePrivateField(component, stateField);
            object syncRoot = syncField.GetValue(component)
                ?? throw new InvalidOperationException(component.GetType().FullName + "." + syncRootField + " is empty.");
            lock (syncRoot)
            {
                state = stateValueField.GetValue(component) as string
                    ?? throw new InvalidOperationException(component.GetType().FullName + "." + stateField + " is not text.");
            }

            if (expectedPrefixes.Any(prefix => state.StartsWith(prefix, StringComparison.Ordinal)))
            {
                return state;
            }

            System.Threading.Thread.Sleep(25);
        }

        throw new TimeoutException(
            componentGuid + " did not reach an expected active or terminal state within "
                + timeout.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " seconds. Last state: " + state + ". " + RuntimeMessages(document, componentGuid));
    }

    private static RuntimeComponentSnapshot ReadRuntimeSnapshot(
        GH_Document document,
        Guid componentGuid,
        string syncRootField,
        string activeTaskField,
        string stateField)
    {
        GH_Component component = RequireObject<GH_Component>(document, componentGuid);
        FieldInfo syncField = RequirePrivateField(component, syncRootField);
        FieldInfo taskField = RequirePrivateField(component, activeTaskField);
        FieldInfo stateValueField = RequirePrivateField(component, stateField);
        object syncRoot = syncField.GetValue(component)
            ?? throw new InvalidOperationException(component.GetType().FullName + "." + syncRootField + " is empty.");
        lock (syncRoot)
        {
            string state = stateValueField.GetValue(component) as string
                ?? throw new InvalidOperationException(component.GetType().FullName + "." + stateField + " is not text.");
            return new RuntimeComponentSnapshot(state, taskField.GetValue(component) is Task);
        }
    }

    private static FieldInfo RequirePrivateField(GH_Component component, string fieldName)
    {
        return component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(component.GetType().FullName, fieldName);
    }

    private static int ReadBatchCacheHits(GH_Document document, Guid componentGuid)
    {
        GH_Component component = RequireObject<GH_Component>(document, componentGuid);
        FieldInfo syncField = RequirePrivateField(component, "_syncRoot");
        FieldInfo progressField = RequirePrivateField(component, "_latestProgress");
        object syncRoot = syncField.GetValue(component)
            ?? throw new InvalidOperationException(component.GetType().FullName + "._syncRoot is empty.");
        lock (syncRoot)
        {
            object progress = progressField.GetValue(component)
                ?? throw new InvalidOperationException("The completed batch exposed no progress snapshot.");
            PropertyInfo property = progress.GetType().GetProperty("CacheHits", BindingFlags.Instance | BindingFlags.Public)
                ?? throw new MissingMemberException(progress.GetType().FullName, "CacheHits");
            return Convert.ToInt32(
                property.GetValue(progress),
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static void BestEffortCancelAndDrain(
        GH_Document document,
        RuntimeWorkflowExpectation expectation,
        TimeSpan workflowTimeout)
    {
        TimeSpan drainTimeout = TimeSpan.FromSeconds(Math.Max(5, Math.Min(30, workflowTimeout.TotalSeconds)));
        try
        {
            SetBoolean(document, expectation.RunTriggerGuid, false);
            SetBoolean(document, expectation.CancelTriggerGuid, false);
            SetBoolean(document, expectation.ForceRerunGuid, false);
            SetBoolean(document, expectation.ExportTriggerGuid, false);
            SetBoolean(document, expectation.OverwriteGuid, false);
            SetBoolean(document, expectation.BatchRunTriggerGuid, false);
            SetBoolean(document, expectation.BatchCancelTriggerGuid, false);
            Solve(document);

            RuntimeComponentSnapshot run = ReadRuntimeSnapshot(
                document,
                expectation.RunComponentGuid,
                "syncRoot",
                "activeTask",
                "stateText");
            RuntimeComponentSnapshot batch = ReadRuntimeSnapshot(
                document,
                expectation.BatchComponentGuid,
                "_syncRoot",
                "_activeTask",
                "_state");
            if (run.HasActiveTask)
            {
                SetBoolean(document, expectation.CancelTriggerGuid, true);
            }

            if (batch.HasActiveTask)
            {
                SetBoolean(document, expectation.BatchCancelTriggerGuid, true);
            }

            if (run.HasActiveTask || batch.HasActiveTask)
            {
                Solve(document);
                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed < drainTimeout)
                {
                    run = ReadRuntimeSnapshot(
                        document,
                        expectation.RunComponentGuid,
                        "syncRoot",
                        "activeTask",
                        "stateText");
                    batch = ReadRuntimeSnapshot(
                        document,
                        expectation.BatchComponentGuid,
                        "_syncRoot",
                        "_activeTask",
                        "_state");
                    if (!run.HasActiveTask && !batch.HasActiveTask)
                    {
                        break;
                    }

                    System.Threading.Thread.Sleep(25);
                }

                if (run.HasActiveTask || batch.HasActiveTask)
                {
                    Console.Error.WriteLine(
                        "Runtime workflow cleanup could not drain every active task within "
                            + drainTimeout.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " seconds.");
                }
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Runtime workflow cleanup failed: " + exception.Message);
        }
        finally
        {
            try
            {
                SetBoolean(document, expectation.RunTriggerGuid, false);
                SetBoolean(document, expectation.CancelTriggerGuid, false);
                SetBoolean(document, expectation.ForceRerunGuid, false);
                SetBoolean(document, expectation.ExportTriggerGuid, false);
                SetBoolean(document, expectation.OverwriteGuid, false);
                SetBoolean(document, expectation.BatchRunTriggerGuid, false);
                SetBoolean(document, expectation.BatchCancelTriggerGuid, false);
                Solve(document);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Runtime workflow trigger reset failed: " + exception.Message);
            }
        }
    }

    private static void RequireSingleBatchCase(
        GH_Document document,
        Guid componentGuid,
        string expectedStatus)
    {
        string[] caseIds = ReadStrings(document, componentGuid, 1);
        string[] caseStatuses = ReadStrings(document, componentGuid, 2);
        Require(
            caseIds.Length == 1 && string.Equals(caseIds[0], "two-zone-office", StringComparison.Ordinal),
            "The single-case example batch did not preserve its ordered case ID.");
        Require(
            caseStatuses.Length == 1 && string.Equals(caseStatuses[0], expectedStatus, StringComparison.Ordinal),
            "The single-case example batch status was not " + expectedStatus + ".");
    }

    private static void RequireSummaryCsvMatchesResult(
        string content,
        double expectedTotalArea,
        double expectedAnnualResult)
    {
        string[][] rows = ReadSimpleCsvRows(content, "SimpleDragon summary CSV");
        string[] expectedHeader =
        {
            "case_id",
            "metric",
            "basis",
            "total_area_m2",
            "annual_total",
            "value_unit",
        };
        Require(
            rows[0].SequenceEqual(expectedHeader, StringComparer.Ordinal),
            "The SimpleDragon summary CSV header changed unexpectedly.");
        Require(rows.Length > 1, "The SimpleDragon summary CSV contains no data rows.");

        string[]? sitePerArea = null;
        foreach (string[] row in rows.Skip(1))
        {
            Require(row.Length == expectedHeader.Length, "The SimpleDragon summary CSV contains a malformed row.");
            Require(
                string.Equals(row[0], "two-zone-office", StringComparison.Ordinal),
                "The SimpleDragon summary CSV contains an unexpected case ID.");
            double totalArea = ParseFiniteCsvNumber(row[3], "summary total_area_m2");
            ParseFiniteCsvNumber(row[4], "summary annual_total");
            RequireNearlyEqual(totalArea, expectedTotalArea, "summary total_area_m2");
            if (string.Equals(row[1], "site_uses", StringComparison.Ordinal)
                && string.Equals(row[2], "per_area", StringComparison.Ordinal))
            {
                Require(sitePerArea is null, "The SimpleDragon summary CSV duplicated site_uses/per_area.");
                sitePerArea = row;
            }
        }

        Require(sitePerArea is not null, "The SimpleDragon summary CSV contains no site_uses/per_area row.");
        RequireNearlyEqual(
            ParseFiniteCsvNumber(sitePerArea![4], "summary site_uses/per_area annual_total"),
            expectedAnnualResult,
            "summary site_uses/per_area annual_total");
    }

    private static void RequireBatchCsvMatchesResult(
        string content,
        double expectedTotalArea,
        double expectedAnnualResult)
    {
        string[][] rows = ReadSimpleCsvRows(content, "SimpleDragon batch combined CSV");
        Require(rows.Length == 2, "The single-case batch combined CSV must contain exactly one data row.");
        string[] header = rows[0];
        string[] row = rows[1];
        Require(row.Length == header.Length, "The batch combined CSV contains a malformed row.");

        int caseIndex = Array.IndexOf(header, "case_id");
        int statusIndex = Array.IndexOf(header, "status");
        int annualIndex = Array.IndexOf(header, "site_energy_per_m2");
        int totalAreaIndex = Array.IndexOf(header, "total_area_m2");
        Require(
            caseIndex >= 0 && statusIndex >= 0 && annualIndex >= 0 && totalAreaIndex >= 0,
            "The batch combined CSV is missing required identity, status, or GRR metric columns.");
        Require(
            string.Equals(row[caseIndex], "two-zone-office", StringComparison.Ordinal)
                && string.Equals(row[statusIndex], "Succeeded", StringComparison.Ordinal),
            "The batch combined CSV does not identify the successful two-zone case.");

        string[] numericColumns =
        {
            "carbon_gross",
            "carbon_per_m2",
            "cost_gross",
            "cost_per_m2",
            "site_energy_gross",
            "site_energy_per_m2",
            "source_energy_gross",
            "source_energy_per_m2",
            "total_area_m2",
        };
        foreach (string column in numericColumns)
        {
            int index = Array.IndexOf(header, column);
            Require(index >= 0, "The batch combined CSV is missing numeric column " + column + ".");
            ParseFiniteCsvNumber(row[index], "batch " + column);
        }

        RequireNearlyEqual(
            ParseFiniteCsvNumber(row[annualIndex], "batch site_energy_per_m2"),
            expectedAnnualResult,
            "batch site_energy_per_m2");
        RequireNearlyEqual(
            ParseFiniteCsvNumber(row[totalAreaIndex], "batch total_area_m2"),
            expectedTotalArea,
            "batch total_area_m2");
    }

    private static string[][] ReadSimpleCsvRows(string content, string label)
    {
        string[][] rows = content
            .Split(CsvLineSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(','))
            .ToArray();
        Require(rows.Length > 0, label + " is empty.");
        return rows;
    }

    private static double ParseFiniteCsvNumber(string text, string label)
    {
        bool parsed = double.TryParse(
            text,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double value);
        Require(parsed && IsFinite(value), label + " is not a finite invariant number: " + text + ".");
        return value;
    }

    private static void RequireNearlyEqual(double actual, double expected, string label)
    {
        double tolerance = Math.Max(1e-6, Math.Abs(expected) * 1e-9);
        Require(
            Math.Abs(actual - expected) <= tolerance,
            label + " was "
                + actual.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " instead of "
                + expected.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + ".");
    }

    private static string RequireFreshContainedFile(
        string candidate,
        string root,
        DateTime notBeforeUtc,
        string label)
    {
        string fullPath = Path.GetFullPath(candidate);
        Require(IsSameOrDescendant(fullPath, root), label + " escaped its requested root: " + fullPath);
        Require(File.Exists(fullPath), label + " was not written: " + fullPath);
        var info = new FileInfo(fullPath);
        Require(info.Length > 0, label + " is empty: " + fullPath);
        Require(info.LastWriteTimeUtc >= notBeforeUtc, label + " was not freshly written: " + fullPath);
        return fullPath;
    }

    private static void RequireContainedDirectory(string candidate, string root, string label)
    {
        string fullPath = Path.GetFullPath(candidate);
        Require(IsSameOrDescendant(fullPath, root), label + " escaped its requested root: " + fullPath);
        Require(Directory.Exists(fullPath), label + " does not exist: " + fullPath);
    }

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        string normalizedCandidate = Path.GetFullPath(candidate)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    private static void SetPanel(GH_Document document, Guid instanceGuid, string value)
    {
        GH_Panel panel = RequireObject<GH_Panel>(document, instanceGuid);
        panel.UserText = value;
        panel.ExpireSolution(false);
    }

    private static void SetBoolean(GH_Document document, Guid instanceGuid, bool value)
    {
        Param_Boolean parameter = RequireObject<Param_Boolean>(document, instanceGuid);
        parameter.PersistentData.Clear();
        parameter.PersistentData.Append(new GH_Boolean(value));
        parameter.ExpireSolution(false);
    }

    private static void RequirePersistentBoolean(GH_Document document, Guid instanceGuid, bool expected)
    {
        Param_Boolean parameter = RequireObject<Param_Boolean>(document, instanceGuid);
        GH_Boolean value = parameter.PersistentData.AllData(true).OfType<GH_Boolean>().SingleOrDefault()
            ?? throw new InvalidOperationException(instanceGuid + " has no single persistent Boolean value.");
        Require(
            value.Value == expected,
            instanceGuid + " was saved " + value.Value + " instead of the safe value " + expected + ".");
    }

    private static void RequireOutputType(
        GH_Document document,
        Guid componentGuid,
        int outputIndex,
        string expectedType)
    {
        object value = ResolveParam(document, componentGuid, outputIndex, output: true)
            .VolatileData
            .AllData(true)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(componentGuid + " output " + outputIndex + " produced no value.");
        Require(
            string.Equals(value.GetType().FullName, expectedType, StringComparison.Ordinal),
            componentGuid + " output " + outputIndex + " produced " + value.GetType().FullName
                + " instead of " + expectedType + ".");
    }

    private static bool ReadBoolean(GH_Document document, Guid componentGuid, int outputIndex)
    {
        object value = ResolveParam(document, componentGuid, outputIndex, output: true)
            .VolatileData
            .AllData(true)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                componentGuid + " output " + outputIndex + " produced no Boolean. "
                    + RuntimeMessages(document, componentGuid));
        return value is GH_Boolean boolean
            ? boolean.Value
             : throw new InvalidOperationException(componentGuid + " output " + outputIndex + " is not a Boolean.");
    }

    private static int ReadInteger(GH_Document document, Guid componentGuid, int outputIndex)
    {
        object value = ResolveParam(document, componentGuid, outputIndex, output: true)
            .VolatileData
            .AllData(true)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(componentGuid + " output " + outputIndex + " produced no integer.");
        return value is GH_Integer integer
            ? integer.Value
            : throw new InvalidOperationException(componentGuid + " output " + outputIndex + " is not an integer.");
    }

    private static double ReadNumber(GH_Document document, Guid componentGuid, int outputIndex)
    {
        object value = ResolveParam(document, componentGuid, outputIndex, output: true)
            .VolatileData
            .AllData(true)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(componentGuid + " output " + outputIndex + " produced no number.");
        return value is GH_Number number
            ? number.Value
            : throw new InvalidOperationException(componentGuid + " output " + outputIndex + " is not a number.");
    }

    private static double[] ReadNumbers(GH_Document document, Guid componentGuid, int outputIndex)
    {
        object[] values = ResolveParam(document, componentGuid, outputIndex, output: true)
            .VolatileData
            .AllData(true)
            .ToArray();
        Require(
            values.All(value => value is GH_Number),
            componentGuid + " output " + outputIndex + " contains a non-numeric value.");
        return values.Cast<GH_Number>().Select(value => value.Value).ToArray();
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static string ReadString(GH_Document document, Guid componentGuid, int outputIndex)
    {
        object value = ResolveParam(document, componentGuid, outputIndex, output: true)
            .VolatileData
            .AllData(true)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                componentGuid + " output " + outputIndex + " produced no text. "
                    + RuntimeMessages(document, componentGuid));
        return value is GH_String text
            ? text.Value
            : throw new InvalidOperationException(componentGuid + " output " + outputIndex + " is not text.");
    }

    private static string[] ReadStrings(GH_Document document, Guid componentGuid, int outputIndex)
    {
        object[] values = ResolveParam(document, componentGuid, outputIndex, output: true)
            .VolatileData
            .AllData(true)
            .ToArray();
        Require(
            values.All(value => value is GH_String),
            componentGuid + " output " + outputIndex + " contains a non-text value.");
        return values.Cast<GH_String>().Select(value => value.Value).ToArray();
    }

    private static string RuntimeMessages(GH_Document document, Guid componentGuid)
    {
        GH_ActiveObject component = RequireObject<GH_ActiveObject>(document, componentGuid);
        string[] messages = Enum.GetValues(typeof(GH_RuntimeMessageLevel))
            .Cast<GH_RuntimeMessageLevel>()
            .SelectMany(level => component.RuntimeMessages(level))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return messages.Length == 0 ? "No component runtime messages." : string.Join(" | ", messages);
    }

    private static void Solve(GH_Document document)
    {
        GH_Document.EnableSolutions = true;
        document.Enabled = true;
        document.NewSolution(true, GH_SolutionMode.Silent);
    }

    private static void ValidateOutwardEnvelope(
        GH_Document document,
        OutwardEnvelopeExpectation expectation)
    {
        foreach (Guid curveParameterGuid in expectation.CurveParameterGuids)
        {
            Param_Curve parameter = RequireObject<Param_Curve>(document, curveParameterGuid);
            GH_Curve goo = parameter.VolatileData.AllData(true).OfType<GH_Curve>().Single();
            Curve curve = goo.Value
                ?? throw new InvalidOperationException(curveParameterGuid + " contains an empty curve.");
            Require(curve.TryGetPolyline(out Polyline polyline), curveParameterGuid + " must remain a polyline.");
            int vertexCount = polyline.IsClosed ? polyline.Count - 1 : polyline.Count;
            Require(vertexCount >= 3, curveParameterGuid + " has fewer than three polygon vertices.");
            var normal = new Vector3d();
            for (int index = 0; index < vertexCount; index++)
            {
                Point3d current = polyline[index];
                Point3d next = polyline[(index + 1) % vertexCount];
                normal.X += (current.Y - next.Y) * (current.Z + next.Z);
                normal.Y += (current.Z - next.Z) * (current.X + next.X);
                normal.Z += (current.X - next.X) * (current.Y + next.Y);
            }

            Require(normal.Unitize(), curveParameterGuid + " has a degenerate polygon normal.");
            using AreaMassProperties properties = AreaMassProperties.Compute(curve)
                ?? throw new InvalidOperationException(curveParameterGuid + " has no planar area centroid.");
            Vector3d fromZoneCentroid = properties.Centroid - expectation.ZoneCentroid;
            Require(
                Vector3d.Multiply(normal, fromZoneCentroid) > 1e-8,
                curveParameterGuid + " points inward relative to the zone centroid.");
        }
    }

    private static int CountWires(GH_Document document)
    {
        return document.Objects.Sum(value => value switch
        {
            GH_Component component => component.Params.Input.Sum(input => input.SourceCount),
            IGH_Param parameter => parameter.SourceCount,
            _ => 0,
        });
    }

    private static IGH_Param ResolveParam(
        GH_Document document,
        Guid objectGuid,
        int? index,
        bool output)
    {
        IGH_DocumentObject value = document.FindObject(objectGuid, topLevelOnly: true)
            ?? throw new InvalidOperationException("Definition lost object " + objectGuid + ".");
        if (index is null)
        {
            return value as IGH_Param
                ?? throw new InvalidOperationException(objectGuid + " is not a parameter.");
        }

        GH_Component component = value as GH_Component
            ?? throw new InvalidOperationException(objectGuid + " is not a component.");
        return output ? component.Params.Output[index.Value] : component.Params.Input[index.Value];
    }

    private static T RequireObject<T>(GH_Document document, Guid instanceGuid)
        where T : class, IGH_DocumentObject
    {
        IGH_DocumentObject value = document.FindObject(instanceGuid, topLevelOnly: true)
            ?? throw new InvalidOperationException("Definition lost object " + instanceGuid + ".");
        return value as T
            ?? throw new InvalidOperationException(instanceGuid + " is not " + typeof(T).FullName + ".");
    }

    private static ExampleDefinitionResult Result(
        AdvancedDefinition definition,
        string path,
        ValidationFacts facts,
        bool generated)
    {
        return new ExampleDefinitionResult
        {
            Product = definition.Product,
            FileName = definition.FileName,
            CanonicalPath = Path.GetFullPath(path),
            Sha256 = ComputeSha256(path),
            ObjectCount = facts.ObjectCount,
            WireCount = facts.WireCount,
            OutputGooType = facts.OutputGooType,
            Generated = generated,
            RuntimeGateStatus = facts.Runtime.GateStatus,
            RuntimeGateReason = facts.Runtime.GateReason,
            RuntimeExecuted = facts.Runtime.Executed,
            RuntimeState = facts.Runtime.State,
            RuntimeResultVerified = facts.Runtime.ResultVerified,
            RuntimeCsvVerified = facts.Runtime.CsvVerified,
            RuntimeCacheVerified = facts.Runtime.CacheVerified,
            RuntimeCancellationVerified = facts.Runtime.CancellationVerified,
            RuntimeBatchVerified = facts.Runtime.BatchVerified,
            RuntimeFirstRunState = facts.Runtime.FirstRunState,
            RuntimeCachedRunState = facts.Runtime.CachedRunState,
            RuntimeCancellationState = facts.Runtime.CancellationState,
            RuntimeFirstBatchState = facts.Runtime.FirstBatchState,
            RuntimeCachedBatchState = facts.Runtime.CachedBatchState,
            RuntimeBatchCancellationState = facts.Runtime.BatchCancellationState,
            RuntimeBatchCancellationVerified = facts.Runtime.BatchCancellationVerified,
            RuntimeEvidenceDirectory = facts.Runtime.EvidenceDirectory,
            RuntimeAnnualResult = facts.Runtime.AnnualResult,
            RuntimeCsvSha256 = facts.Runtime.CsvSha256,
            RuntimeBatchCombinedCsvSha256 = facts.Runtime.BatchCombinedCsvSha256,
            RuntimeBatchManifestSha256 = facts.Runtime.BatchManifestSha256,
            RuntimeBatchCancellationCsvSha256 = facts.Runtime.BatchCancellationCsvSha256,
            RuntimeBatchCancellationManifestSha256 = facts.Runtime.BatchCancellationManifestSha256,
        };
    }

    private static void Save(GH_Document document, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var writer = new GH_DocumentIO(document);
        Require(writer.SaveQuiet(path), "Grasshopper failed to save " + path + ".");
        Require(File.Exists(path), "Grasshopper reported a save but the file is absent: " + path + ".");
    }

    private static string StagedDefinitionPath(
        string outputDirectory,
        string stage,
        string fileName,
        string examplesRoot)
    {
        string stagedRepository = Path.Combine(outputDirectory, stage, "repository");
        string stagedExamples = Path.Combine(stagedRepository, "examples");
        string stagedFixture = Path.Combine(
            stagedRepository,
            "fixtures",
            "simple-dragon",
            "grr",
            "ASHRAE 140 modified.grr");
        Directory.CreateDirectory(stagedExamples);
        Directory.CreateDirectory(Path.GetDirectoryName(stagedFixture)!);
        string repositoryRoot = Directory.GetParent(Path.GetFullPath(examplesRoot))?.FullName
            ?? throw new InvalidOperationException("The repository root could not be resolved from examples.");
        string sourceFixture = Path.Combine(
            repositoryRoot,
            "fixtures",
            "simple-dragon",
            "grr",
            "ASHRAE 140 modified.grr");
        File.Copy(sourceFixture, stagedFixture, overwrite: true);
        return Path.Combine(stagedExamples, fileName);
    }

    private static GH_Document Open(string path)
    {
        var reader = new GH_DocumentIO();
        Require(reader.Open(path), "Grasshopper failed to open " + path + ".");
        return reader.Document
            ?? throw new InvalidOperationException("Grasshopper opened a document without content: " + path + ".");
    }

    private static string ComputeSha256(string path)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return string.Concat(sha256.ComputeHash(stream).Select(value =>
            value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static PolylineCurve ClosedCurve(IEnumerable<Point3d> vertices)
    {
        Point3d[] points = vertices.ToArray();
        return new PolylineCurve(points.Concat(new[] { points[0] }));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record AdvancedDefinition(
        string Product,
        string FileName,
        Func<GH_ComponentServer, ScenarioGraph> Build);

    private sealed record ValidationFacts(
        int ObjectCount,
        int WireCount,
        string OutputGooType,
        RuntimeValidationFacts Runtime);

    private sealed record RuntimeComponentSnapshot(string State, bool HasActiveTask);

    private sealed record RuntimeValidationFacts(
        string GateStatus,
        string GateReason,
        bool Executed,
        string State,
        string FirstRunState,
        string CachedRunState,
        string CancellationState,
        string FirstBatchState,
        string CachedBatchState,
        string BatchCancellationState,
        bool ResultVerified,
        bool CsvVerified,
        bool CacheVerified,
        bool CancellationVerified,
        bool BatchVerified,
        bool BatchCancellationVerified,
        string EvidenceDirectory,
        double? AnnualResult,
        string[] CsvSha256,
        string BatchCombinedCsvSha256,
        string BatchManifestSha256,
        string BatchCancellationCsvSha256,
        string BatchCancellationManifestSha256);

    private static class Catalog
    {
        internal static readonly ComponentIdentity InvisibleMaterial = I("dca742da-0ac5-4520-8022-97f98974dfea", "OpaqueMaterialComponent");
        internal static readonly ComponentIdentity InvisibleConstruction = I("6d5a9b54-8a9e-4c95-91df-469e21a783c9", "LayeredConstructionComponent");
        internal static readonly ComponentIdentity InvisibleNoMass = I("e292a44e-9d8d-4796-95fb-126f77e83796", "NoMassConstructionComponent");
        internal static readonly ComponentIdentity InvisibleProfile = I("3d5717de-1b16-406a-91e0-7a392c08aa51", "ConstantProfileComponent");
        internal static readonly ComponentIdentity InvisibleSurface = I("291150ba-bbb5-41c2-99ac-914a5183d3ed", "SurfaceFromPolylineComponent");
        internal static readonly ComponentIdentity InvisibleZone = I("e5627899-dcdb-4154-98fc-f7c547d50d2e", "ZoneComponent");
        internal static readonly ComponentIdentity InvisibleHeatPump = I("e8751fda-24b9-4727-ad66-f81de722f64f", "HeatPumpComponent");
        internal static readonly ComponentIdentity InvisibleAirHandler = I("a3a4afd8-17e1-4d9f-8da5-5883331c360f", "AirHandlingUnitComponent");
        internal static readonly ComponentIdentity InvisibleBoiler = I("e732f5f9-db94-405b-9221-f4449b4baad7", "BoilerComponent");
        internal static readonly ComponentIdentity InvisibleRadiantFloor = I("e3bd88b6-54b6-43ec-9c94-ee0e36218618", "RadiantFloorComponent");
        internal static readonly ComponentIdentity InvisibleErv = I("3d5f630e-66c3-43da-b73c-50d5be1792c3", "EnergyRecoveryVentilatorComponent");
        internal static readonly ComponentIdentity InvisiblePv = I("237bc85d-769a-468b-a048-70e3b5c382ee", "PhotovoltaicPanelComponent");
        internal static readonly ComponentIdentity InvisibleModel = I("fee2629c-94d8-4eed-8be2-14ba108ce825", "EnergyModelComponent");
        internal static readonly ComponentIdentity InvisibleCompile = I("2743be88-ef3a-4f0d-abf8-cf062d93aafe", "CompileIdfComponent");
        internal static readonly ComponentIdentity InvisibleValidate = I("fa664eeb-5503-4366-831d-e3478c8a1832", "ValidateIdfComponent");
        internal static readonly ComponentIdentity InvisibleRun = I("5f1a9663-6f81-4635-b54d-607b48c9fd47", "RunEnergyPlusComponent");
        internal static readonly ComponentIdentity InvisibleResultSummary = I("31967aee-84ae-4536-b091-b301d1ab2c3d", "EnergyPlusResultSummaryComponent");
        internal static readonly ComponentIdentity SimpleMaterial = S("fee586e8-692c-407e-a803-d5c43f3c7222", "SimpleDragonMaterialComponent");
        internal static readonly ComponentIdentity SimpleConstruction = S("3e1fa67f-dbb2-4c19-b54b-226c295f5751", "SimpleDragonSurfaceConstructionComponent");
        internal static readonly ComponentIdentity SimpleFenestration = S("b9af07b4-d08e-4335-ab55-a6fd33cb1a93", "SimpleDragonFenestrationConstructionComponent");
        internal static readonly ComponentIdentity SimpleProfile = S("fb92c938-41e1-475f-ad03-ca6a1a8e42e1", "LookupUsageProfileComponent");
        internal static readonly ComponentIdentity SimpleHeatPump = S("e6e14d7b-55b4-45a9-97f9-9b99715f5ebc", "SimpleDragonHeatPumpComponent");
        internal static readonly ComponentIdentity SimpleAirHandler = S("8b0839fc-d03d-46af-8897-1ba4a41eab46", "SimpleDragonAirHandlingUnitComponent");
        internal static readonly ComponentIdentity SimpleBoiler = S("7b973e2c-7254-4730-9326-c320abedde5a", "SimpleDragonBoilerComponent");
        internal static readonly ComponentIdentity SimpleRadiator = S("2e77eee2-c354-40ba-abae-b501373046bc", "SimpleDragonRadiatorComponent");
        internal static readonly ComponentIdentity SimpleChiller = S("d5cedc15-8b76-49e3-842b-5b0c498556fd", "SimpleDragonChillerComponent");
        internal static readonly ComponentIdentity SimpleFanCoil = S("dd41df8f-9e3e-4663-8ce7-89025cfde30c", "SimpleDragonFanCoilUnitComponent");
        internal static readonly ComponentIdentity SimpleErv = S("15afd6e6-1c05-4715-909b-b6e98ef91375", "SimpleDragonEnergyRecoveryVentilatorComponent");
        internal static readonly ComponentIdentity SimplePv = S("7fcb5c47-3d49-4aa0-8fbc-bd765711401f", "SimpleDragonPhotovoltaicPanelComponent");
        internal static readonly ComponentIdentity SimpleExtractZones = S("668591e2-458a-42a2-a924-6c3862f1b2c6", "ExtractSimpleDragonZonesComponent");
        internal static readonly ComponentIdentity SimpleAssignSupplies = S("82b8b48c-5930-4649-bc5f-6c17b05daa52", "AssignSimpleDragonSupplySystemsComponent");
        internal static readonly ComponentIdentity SimpleAssignVentilation = S("5f66b3fd-e69c-4c33-92db-839c07dcbda5", "AssignSimpleDragonVentilationSystemsComponent");
        internal static readonly ComponentIdentity SimpleAssemble = S("f0a131e0-7cfe-45fc-945a-7e52237535ee", "AssembleGreenRetrofitModelComponent");
        internal static readonly ComponentIdentity SimpleConvert = S("b38f2e41-f63b-42a8-b549-65cd60c7a994", "ConvertGreenRetrofitModelComponent");
        internal static readonly ComponentIdentity SimpleBuildResult = S("2a9f3a4e-56f2-4227-8725-e8befe43cf53", "BuildGreenRetrofitResultComponent");
        internal static readonly ComponentIdentity SimpleReadResult = S("a03fb1d7-7ae2-4e2c-ab31-0e626af50163", "ReadGreenRetrofitResultComponent");
        internal static readonly ComponentIdentity SimpleResultSummary = S("577809aa-2d1c-40ea-aa50-f71d73f19f83", "GreenRetrofitResultSummaryComponent");
        internal static readonly ComponentIdentity SimpleDataTree = S("cb5a98f8-4188-4323-b55d-795b4a7ba20e", "GreenRetrofitDataTreeComponent");
        internal static readonly ComponentIdentity SimpleLinePlot = S("76e0c1b6-68d6-4cdc-a418-eea18aa131c1", "GreenRetrofitMonthlyLinePlotComponent");
        internal static readonly ComponentIdentity SimpleBarPlot = S("a73acba4-d98d-4fec-a846-dc982256d6b1", "GreenRetrofitMonthlyBarPlotComponent");
        internal static readonly ComponentIdentity SimpleExportCsv = S("9fe8a410-ea95-4eb8-81ec-56c45cdd029c", "ExportGreenRetrofitCsvComponent");
        internal static readonly ComponentIdentity SimpleBatch = S("c0af86b6-5f6e-478c-b069-a7892a31dadd", "RunSimpleDragonBatchComponent");

        internal static IReadOnlyList<ComponentIdentity> All { get; } = new[]
        {
            InvisibleMaterial, InvisibleConstruction, InvisibleNoMass, InvisibleProfile, InvisibleSurface,
            InvisibleZone, InvisibleHeatPump, InvisibleAirHandler, InvisibleBoiler, InvisibleRadiantFloor,
            InvisibleErv, InvisiblePv, InvisibleModel, InvisibleCompile, InvisibleValidate, InvisibleRun,
            InvisibleResultSummary,
            SimpleMaterial, SimpleConstruction, SimpleFenestration, SimpleProfile, SimpleHeatPump,
            SimpleAirHandler, SimpleBoiler, SimpleRadiator, SimpleChiller, SimpleFanCoil, SimpleErv,
            SimplePv, SimpleExtractZones, SimpleAssignSupplies, SimpleAssignVentilation, SimpleAssemble,
            SimpleConvert, SimpleBuildResult, SimpleReadResult, SimpleResultSummary, SimpleDataTree,
            SimpleLinePlot, SimpleBarPlot, SimpleExportCsv, SimpleBatch,
        };

        private static ComponentIdentity I(string id, string type)
        {
            return new ComponentIdentity(
                InvisibleProduct,
                new Guid(id),
                "GonieGonie.InvisibleDragon.Grasshopper.Components." + type);
        }

        private static ComponentIdentity S(string id, string type)
        {
            return new ComponentIdentity(
                SimpleProduct,
                new Guid(id),
                "GonieGonie.SimpleDragon.Grasshopper.Components." + type);
        }
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Ownership of the Grasshopper document is transferred to ScenarioGraph.")]
internal sealed class ScenarioGraphBuilder
{
    private readonly GH_ComponentServer _server;
    private readonly string _instancePrefix;
    private readonly GH_Document _document = new();
    private readonly List<ObjectExpectation> _objects = new();
    private readonly List<WireExpectation> _wires = new();
    private readonly List<OutputExpectation> _outputs = new();
    private readonly List<BooleanExpectation> _booleans = new();
    private readonly List<NumberExpectation> _numbers = new();

    internal ScenarioGraphBuilder(GH_ComponentServer server, string instancePrefix)
    {
        _server = server;
        _instancePrefix = instancePrefix;
    }

    internal GraphNode Component(int key, ComponentIdentity identity, float x, float y)
    {
        IGH_DocumentObject value = _server.EmitObject(identity.Id)
            ?? throw new InvalidOperationException("Grasshopper could not emit " + identity.Id + ".");
        if (value is not GH_Component component)
        {
            throw new InvalidOperationException(identity.Id + " is not a Grasshopper component.");
        }

        if (!string.Equals(component.GetType().FullName, identity.TypeName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                identity.Id + " emitted " + component.GetType().FullName + " instead of " + identity.TypeName + ".");
        }

        GraphNode node = Add(key, component, x, y);
        _objects.Add(new ObjectExpectation(node.InstanceGuid, identity.TypeName, identity.Id));
        return node;
    }

    internal GraphNode Panel(int key, string nickName, string text, float x, float y)
    {
        var panel = new GH_Panel
        {
            NickName = nickName,
            UserText = text,
        };
        return AddSpecial(key, panel, x, y);
    }

    internal GraphNode Slider(
        int key,
        string nickName,
        decimal value,
        decimal minimum,
        decimal maximum,
        float x,
        float y)
    {
        var slider = new GH_NumberSlider { NickName = nickName };
        slider.Slider.Minimum = minimum;
        slider.Slider.Maximum = maximum;
        slider.Slider.DecimalPlaces = 3;
        slider.Slider.Value = value;
        return AddSpecial(key, slider, x, y);
    }

    internal GraphNode Breps(int key, string nickName, IEnumerable<Brep> values, float x, float y)
    {
        var parameter = new Param_Brep { NickName = nickName };
        foreach (Brep value in values)
        {
            parameter.PersistentData.Append(new GH_Brep(value));
        }

        return AddSpecial(key, parameter, x, y);
    }

    internal GraphNode Curves(int key, string nickName, IEnumerable<Curve> values, float x, float y)
    {
        var parameter = new Param_Curve { NickName = nickName };
        foreach (Curve value in values)
        {
            parameter.PersistentData.Append(new GH_Curve(value));
        }

        return AddSpecial(key, parameter, x, y);
    }

    internal GraphNode Integers(int key, string nickName, IEnumerable<int> values, float x, float y)
    {
        var parameter = new Param_Integer { NickName = nickName };
        foreach (int value in values)
        {
            parameter.PersistentData.Append(new GH_Integer(value));
        }

        return AddSpecial(key, parameter, x, y);
    }

    internal GraphNode Boolean(int key, string nickName, bool value, float x, float y)
    {
        var parameter = new Param_Boolean { NickName = nickName };
        parameter.PersistentData.Append(new GH_Boolean(value));
        return AddSpecial(key, parameter, x, y);
    }

    internal GraphNode FilePath(int key, string nickName, string value, float x, float y)
    {
        var parameter = new Param_FilePath
        {
            NickName = nickName,
            ExpireOnFileEvent = false,
            FileFilter = "SimpleDragon result (*.grr)|*.grr|All files (*.*)|*.*",
        };
        parameter.PersistentData.Append(new GH_String(value));
        return AddSpecial(key, parameter, x, y);
    }

    internal void Connect(GraphNode source, int? sourceOutput, GraphNode target, int? targetInput)
    {
        IGH_Param sourceParam = Parameter(source.Object, sourceOutput, output: true);
        IGH_Param targetParam = Parameter(target.Object, targetInput, output: false);
        targetParam.AddSource(sourceParam);
        _wires.Add(new WireExpectation(
            source.InstanceGuid,
            sourceOutput,
            new TargetKey(target.InstanceGuid, targetInput)));
    }

    internal void ExpectOutput(GraphNode node, int? outputIndex, int minimumCount, string? gooType = null)
    {
        _outputs.Add(new OutputExpectation(node.InstanceGuid, outputIndex, minimumCount, gooType));
    }

    internal void ExpectBoolean(GraphNode node, int outputIndex, bool expected)
    {
        _booleans.Add(new BooleanExpectation(node.InstanceGuid, outputIndex, expected));
    }

    internal void ExpectNumber(GraphNode node, int outputIndex, double expected, double tolerance)
    {
        _numbers.Add(new NumberExpectation(node.InstanceGuid, outputIndex, expected, tolerance));
    }

    internal ScenarioGraph Build(
        string product,
        string primaryOutputGooType,
        LinkedModelExpectation? linkedModel = null,
        OutwardEnvelopeExpectation? envelope = null,
        RuntimeWorkflowExpectation? runtimeWorkflow = null)
    {
        return new ScenarioGraph(
            product,
            _document,
            _objects.ToArray(),
            _wires.ToArray(),
            _outputs.ToArray(),
            _booleans.ToArray(),
            _numbers.ToArray(),
            primaryOutputGooType,
            linkedModel,
            envelope,
            runtimeWorkflow);
    }

    private GraphNode AddSpecial<T>(int key, T value, float x, float y)
        where T : class, IGH_DocumentObject
    {
        GraphNode node = Add(key, value, x, y);
        _objects.Add(new ObjectExpectation(node.InstanceGuid, value.GetType().FullName!, null));
        return node;
    }

    private GraphNode Add(int key, IGH_DocumentObject value, float x, float y)
    {
        Guid id = InstanceGuid(key);
        value.NewInstanceGuid(id);
        value.CreateAttributes();
        value.Attributes.Pivot = new System.Drawing.PointF(x, y);
        if (!_document.AddObject(value, update: false, index: _document.ObjectCount))
        {
            throw new InvalidOperationException("Grasshopper refused to add " + value.GetType().FullName + ".");
        }

        return new GraphNode(id, value);
    }

    private Guid InstanceGuid(int key)
    {
        return new Guid(_instancePrefix + "-0000-4000-8000-" + key.ToString("D12", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static IGH_Param Parameter(IGH_DocumentObject value, int? index, bool output)
    {
        if (index is null)
        {
            return value as IGH_Param
                ?? throw new InvalidOperationException(value.GetType().FullName + " is not a parameter.");
        }

        GH_Component component = value as GH_Component
            ?? throw new InvalidOperationException(value.GetType().FullName + " is not a component.");
        return output ? component.Params.Output[index.Value] : component.Params.Input[index.Value];
    }
}

internal sealed record ComponentIdentity(string Product, Guid Id, string TypeName);

internal sealed record GraphNode(Guid InstanceGuid, IGH_DocumentObject Object);

internal sealed record ObjectExpectation(Guid InstanceGuid, string TypeName, Guid? ComponentGuid);

internal sealed record TargetKey(Guid ObjectGuid, int? Index);

internal sealed record WireExpectation(Guid SourceObjectGuid, int? SourceOutputIndex, TargetKey Target);

internal sealed record OutputExpectation(Guid ObjectGuid, int? OutputIndex, int MinimumCount, string? GooType);

internal sealed record BooleanExpectation(Guid ObjectGuid, int OutputIndex, bool Expected);

internal sealed record NumberExpectation(Guid ObjectGuid, int OutputIndex, double Expected, double Tolerance);

internal sealed record LinkedModelExpectation(string FileName, Guid BrepParameterGuid, Guid CurveParameterGuid);

internal sealed record OutwardEnvelopeExpectation(Guid[] CurveParameterGuids, Point3d ZoneCentroid);

internal sealed record RuntimeWorkflowExpectation(
    Guid RunComponentGuid,
    Guid RunTriggerGuid,
    Guid CancelTriggerGuid,
    Guid ForceRerunGuid,
    Guid RuntimeRootGuid,
    Guid WeatherPathGuid,
    Guid TempRootGuid,
    Guid EnergyPlusSummaryGuid,
    Guid BuildResultGuid,
    Guid ResultSummaryGuid,
    Guid ExportCsvGuid,
    Guid ExportDirectoryGuid,
    Guid ExportTriggerGuid,
    Guid OverwriteGuid,
    Guid BatchComponentGuid,
    Guid BatchOutputRootGuid,
    Guid BatchRunTriggerGuid,
    Guid BatchCancelTriggerGuid);

internal sealed record ScenarioGraph(
    string Product,
    GH_Document Document,
    IReadOnlyList<ObjectExpectation> Objects,
    IReadOnlyList<WireExpectation> Wires,
    IReadOnlyList<OutputExpectation> Outputs,
    IReadOnlyList<BooleanExpectation> Booleans,
    IReadOnlyList<NumberExpectation> Numbers,
    string PrimaryOutputGooType,
    LinkedModelExpectation? LinkedModel,
    OutwardEnvelopeExpectation? Envelope,
    RuntimeWorkflowExpectation? RuntimeWorkflow);
