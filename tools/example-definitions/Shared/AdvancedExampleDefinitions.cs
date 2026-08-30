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
    private static readonly string[] CsvLineSeparators = { "\r\n", "\n", "\r" };
    private static readonly string[] SurfaceBoundaryChoices = { "Outdoors", "Ground", "Adiabatic" };
    private static readonly string[] InvisiblePlainWallNames = { "North Wall", "West Wall", "East Wall" };

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
            "12-simpledragon-two-zone-model.gh",
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
        CanonicalExamplePublisher.Publish(
            candidatePath,
            canonicalPath,
            inputs.OutputDirectory,
            path => ValidateGraph(
                Open(path),
                graph,
                inputs,
                exerciseRuntimeWorkflow: false));
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
        GraphNode concreteThickness = graph.Slider(7, "Concrete 0.200 m", 0.2m, 0.01m, 0.5m, 300, 130);
        GraphNode insulationThickness = graph.Slider(8, "Insulation 0.120 m", 0.12m, 0.01m, 0.5m, 300, 270);
        GraphNode finishThickness = graph.Slider(9, "Finish 0.013 m", 0.013m, 0.001m, 0.1m, 300, 410);
        GraphNode concreteLayer = graph.Component(20, Catalog.InvisibleLayer, 520, 60);
        GraphNode insulationLayer = graph.Component(21, Catalog.InvisibleLayer, 520, 200);
        GraphNode finishLayer = graph.Component(22, Catalog.InvisibleLayer, 520, 340);
        GraphNode construction = graph.Component(10, Catalog.InvisibleConstruction, 800, 240);
        GraphNode noMass = graph.Component(11, Catalog.InvisibleNoMass, 650, 700);
        GraphNode profile = graph.Component(12, Catalog.InvisibleProfile, 650, 850);
        GraphNode uValue = graph.Panel(13, "Layered U-value", string.Empty, 980, 270);
        GraphNode layeredValue = graph.Panel(14, "Layered construction", string.Empty, 980, 370);
        GraphNode noMassValue = graph.Panel(15, "No-mass construction", string.Empty, 980, 720);
        GraphNode profileValue = graph.Panel(16, "Annual profile", string.Empty, 980, 870);

        graph.Connect(concreteName, null, concrete, 0);
        graph.Connect(insulationName, null, insulation, 0);
        graph.Connect(finishName, null, finish, 0);
        graph.Connect(concrete, 0, concreteLayer, 0);
        graph.Connect(concreteThickness, null, concreteLayer, 1);
        graph.Connect(insulation, 0, insulationLayer, 0);
        graph.Connect(insulationThickness, null, insulationLayer, 1);
        graph.Connect(finish, 0, finishLayer, 0);
        graph.Connect(finishThickness, null, finishLayer, 1);
        foreach (GraphNode layer in new[] { concreteLayer, insulationLayer, finishLayer })
        {
            graph.Connect(layer, 0, construction, 1);
            graph.ExpectOutput(layer, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonLayerGoo");
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
        GraphNode materialNote = graph.Note(
            800,
            "Define each material and its thickness, then combine the three layer outputs.",
            60,
            -60);
        GraphNode constructionNote = graph.Note(
            801,
            "The ordered layer list becomes one opaque construction with a calculated U-value.",
            760,
            120);
        GraphNode presetNote = graph.Note(
            802,
            "No-mass constructions and annual profiles are independent reusable definitions.",
            620,
            600);
        graph.Group(
            900,
            "1  Materials and layers",
            ExampleGroupTheme.Inputs,
            materialNote,
            concreteName,
            insulationName,
            finishName,
            concrete,
            insulation,
            finish,
            concreteThickness,
            insulationThickness,
            finishThickness,
            concreteLayer,
            insulationLayer,
            finishLayer);
        graph.Group(
            901,
            "2  Layered construction",
            ExampleGroupTheme.Envelope,
            constructionNote,
            construction,
            uValue,
            layeredValue);
        graph.Group(
            902,
            "Reusable presets",
            ExampleGroupTheme.Model,
            presetNote,
            noMass,
            profile,
            noMassValue,
            profileValue);
        return graph.Build(
            InvisibleProduct,
            "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonConstructionGoo");
    }

    private static ScenarioGraph BuildInvisibleSingleZone(GH_ComponentServer server)
    {
        var graph = new ScenarioGraphBuilder(server, "12000000");
        GraphNode material = graph.Component(1, Catalog.InvisibleMaterial, 80, 80);
        GraphNode thickness = graph.Slider(2, "Envelope 0.200 m", 0.2m, 0.01m, 0.5m, 100, 230);
        GraphNode layer = graph.Component(8, Catalog.InvisibleLayer, 360, 120);
        GraphNode construction = graph.Component(3, Catalog.InvisibleConstruction, 650, 120);
        GraphNode profile = graph.Component(4, Catalog.InvisibleProfile, 650, 1050);
        GraphNode glazing = graph.Component(5, Catalog.InvisibleGlazing, 360, 800);
        GraphNode windowBoundary = graph.Curves(
            6,
            "South window boundary",
            new[]
            {
                ClosedCurve(new[]
                {
                    new Point3d(2, 0, 1),
                    new Point3d(6, 0, 1),
                    new Point3d(6, 0, 2.2),
                    new Point3d(2, 0, 2.2),
                }),
            },
            80,
            900);
        GraphNode window = graph.Component(7, Catalog.InvisibleWindow, 650, 850);
        graph.Connect(material, 0, layer, 0);
        graph.Connect(thickness, null, layer, 1);
        graph.Connect(layer, 0, construction, 1);
        graph.Connect(windowBoundary, null, window, 0);
        graph.Connect(glazing, 0, window, 2);

        Point3d[][] polygons =
        {
            new[] { new Point3d(0, 6, 0), new Point3d(8, 6, 0), new Point3d(8, 0, 0), new Point3d(0, 0, 0) },
            new[] { new Point3d(0, 0, 3), new Point3d(8, 0, 3), new Point3d(8, 6, 3), new Point3d(0, 6, 3) },
            new[] { new Point3d(0, 0, 0), new Point3d(8, 0, 0), new Point3d(8, 0, 3), new Point3d(0, 0, 3) },
            new[] { new Point3d(0, 6, 0), new Point3d(0, 6, 3), new Point3d(8, 6, 3), new Point3d(8, 6, 0) },
            new[] { new Point3d(0, 0, 0), new Point3d(0, 0, 3), new Point3d(0, 6, 3), new Point3d(0, 6, 0) },
            new[] { new Point3d(8, 0, 0), new Point3d(8, 6, 0), new Point3d(8, 6, 3), new Point3d(8, 0, 3) },
        };
        GraphNode floorCurve = graph.Curves(10, "Floor boundary", new[] { ClosedCurve(polygons[0]) }, 80, 390);
        GraphNode ceilingCurve = graph.Curves(11, "Ceiling boundary", new[] { ClosedCurve(polygons[1]) }, 80, 540);
        GraphNode plainWallCurves = graph.Curves(
            12,
            "Plain walls (list)",
            new[] { ClosedCurve(polygons[3]), ClosedCurve(polygons[4]), ClosedCurve(polygons[5]) },
            80,
            690);
        GraphNode southWallCurve = graph.Curves(13, "South wall boundary", new[] { ClosedCurve(polygons[2]) }, 80, 840);
        GraphNode floorName = graph.Panel(30, "Floor name", "Floor", 360, 390);
        GraphNode ceilingName = graph.Panel(31, "Ceiling name", "Roof", 360, 540);
        GraphNode plainWallNames = graph.Strings(
            32,
            "Plain wall names (list)",
            InvisiblePlainWallNames,
            360,
            690);
        GraphNode southWallName = graph.Panel(33, "South wall name", "South Wall", 360, 840);
        GraphNode groundBoundary = graph.ValueList(
            40,
            "Floor boundary",
            SurfaceBoundaryChoices,
            "Ground",
            700,
            450);
        GraphNode floor = graph.Component(20, Catalog.InvisibleFloor, 900, 370);
        GraphNode ceiling = graph.Component(21, Catalog.InvisibleCeiling, 900, 520);
        GraphNode plainWalls = graph.Component(22, Catalog.InvisibleWall, 900, 670);
        GraphNode southWall = graph.Component(23, Catalog.InvisibleWall, 900, 820);
        GraphNode[] curves = { floorCurve, ceilingCurve, plainWallCurves, southWallCurve };
        GraphNode[] surfaces = { floor, ceiling, plainWalls, southWall };

        graph.Connect(floorCurve, null, floor, 0);
        graph.Connect(floorName, null, floor, 1);
        graph.Connect(construction, 0, floor, 2);
        graph.Connect(groundBoundary, null, floor, 3);
        graph.Connect(ceilingCurve, null, ceiling, 0);
        graph.Connect(ceilingName, null, ceiling, 1);
        graph.Connect(construction, 0, ceiling, 2);
        graph.Connect(plainWallCurves, null, plainWalls, 0);
        graph.Connect(plainWallNames, null, plainWalls, 1);
        graph.Connect(construction, 0, plainWalls, 2);
        graph.Connect(southWallCurve, null, southWall, 0);
        graph.Connect(southWallName, null, southWall, 1);
        graph.Connect(construction, 0, southWall, 2);
        graph.Connect(window, 0, southWall, 4);
        graph.ExpectOutput(floor, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonSurfaceGoo");
        graph.ExpectOutput(ceiling, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonSurfaceGoo");
        graph.ExpectOutput(plainWalls, 0, 3, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonSurfaceGoo");
        graph.ExpectOutput(southWall, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonSurfaceGoo");

        GraphNode zone = graph.Component(50, Catalog.InvisibleZone, 1230, 1050);
        foreach (GraphNode surface in surfaces)
        {
            graph.Connect(surface, 0, zone, 1);
        }

        graph.Connect(profile, 0, zone, 2);
        GraphNode heatPump = graph.Component(60, Catalog.InvisibleHeatPump, 80, 1280);
        GraphNode airHandler = graph.Component(61, Catalog.InvisibleAirHandler, 620, 1280);
        GraphNode boiler = graph.Component(62, Catalog.InvisibleBoiler, 80, 1480);
        GraphNode radiantFloor = graph.Component(63, Catalog.InvisibleRadiantFloor, 620, 1480);
        GraphNode ventilator = graph.Component(64, Catalog.InvisibleErv, 620, 1680);
        GraphNode photovoltaic = graph.Component(65, Catalog.InvisiblePv, 1230, 1600);
        GraphNode ventilationFlow = graph.Slider(
            66,
            "ERV supply flow 0.20 m3/s",
            0.2m,
            0.01m,
            2m,
            300,
            1740);
        graph.Connect(heatPump, 0, airHandler, 1);
        graph.Connect(boiler, 0, radiantFloor, 1);
        graph.Connect(ventilationFlow, null, ventilator, 3);
        graph.Connect(airHandler, 0, zone, 6);
        graph.Connect(radiantFloor, 0, zone, 6);
        graph.Connect(ventilator, 0, zone, 7);

        GraphNode model = graph.Component(70, Catalog.InvisibleModel, 1560, 1150);
        graph.Connect(zone, 0, model, 1);
        graph.Connect(photovoltaic, 0, model, 4);
        GraphNode compile = graph.Component(71, Catalog.InvisibleCompile, 1840, 500);
        GraphNode idfText = graph.Panel(80, "Compiled IDF", string.Empty, 2120, 330);
        GraphNode valid = graph.Panel(81, "Managed IDF validation", string.Empty, 2120, 460);
        GraphNode diagnostics = graph.Panel(82, "Compilation diagnostics", string.Empty, 2120, 590);
        GraphNode epwPath = graph.EmptyFilePath(83, "EPW File", "EnergyPlus weather (*.epw)|*.epw|All files (*.*)|*.*", 1840, 1400);
        GraphNode weather = graph.Component(84, Catalog.InvisibleWeather, 2120, 1380);
        GraphNode weatherSuccess = graph.Panel(85, "Weather verified", string.Empty, 2400, 1400);
        GraphNode weatherDiagnostics = graph.Panel(86, "Weather diagnostics", string.Empty, 2400, 1530);
        GraphNode runTrigger = graph.Boolean(87, "Run - explicit rising edge", false, 2480, 760);
        GraphNode cancelTrigger = graph.Boolean(88, "Cancel active run", false, 2480, 840);
        GraphNode forceRerun = graph.Boolean(89, "Force rerun", false, 2480, 920);
        GraphNode timeout = graph.Slider(90, "Run timeout 30 min", 30m, 1m, 120m, 2480, 1000);
        GraphNode run = graph.Component(91, Catalog.InvisibleManagedRun, 2800, 850);
        GraphNode result = graph.Panel(92, "EnergyPlus result", string.Empty, 3120, 760);
        GraphNode runState = graph.Panel(93, "InvisibleDragon run state", string.Empty, 3120, 890);
        GraphNode runSuccess = graph.Panel(94, "InvisibleDragon run success", string.Empty, 3120, 1020);
        GraphNode runDiagnostics = graph.Panel(95, "Run diagnostics", string.Empty, 3120, 1150);
        graph.Connect(model, 0, compile, 0);
        graph.Connect(compile, 1, idfText, null);
        graph.Connect(compile, 2, valid, null);
        graph.Connect(compile, 3, diagnostics, null);
        graph.Connect(epwPath, null, weather, 0);
        graph.Connect(weather, 1, weatherSuccess, null);
        graph.Connect(weather, 2, weatherDiagnostics, null);
        graph.Connect(compile, 0, run, 0);
        graph.Connect(weather, 0, run, 1);
        graph.Connect(runTrigger, null, run, 2);
        graph.Connect(cancelTrigger, null, run, 3);
        graph.Connect(forceRerun, null, run, 4);
        graph.Connect(timeout, null, run, 5);
        graph.Connect(run, 0, result, null);
        graph.Connect(run, 1, runState, null);
        graph.Connect(run, 2, runSuccess, null);
        graph.Connect(run, 3, runDiagnostics, null);
        graph.ExpectOutput(glazing, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonGlazingGoo");
        graph.ExpectOutput(layer, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonLayerGoo");
        graph.ExpectOutput(window, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonOpeningGoo");
        graph.ExpectOutput(zone, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonZoneDefinitionGoo");
        graph.ExpectOutput(airHandler, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonSupplySystemGoo");
        graph.ExpectOutput(radiantFloor, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonSupplySystemGoo");
        graph.ExpectOutput(model, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonEnergyModelGoo");
        graph.ExpectOutput(compile, 0, 1, "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonIdfGoo");
        graph.ExpectOutput(idfText, null, 1);
        graph.ExpectOutput(valid, null, 1);
        graph.ExpectBoolean(zone, 1, true);
        graph.ExpectBoolean(model, 1, true);
        graph.ExpectBoolean(compile, 2, true);
        GraphNode constructionNote = graph.Note(
            800,
            "Build one reusable opaque construction before authoring the zone envelope.",
            60,
            10);
        GraphNode envelopeNote = graph.Note(
            801,
            "Floor, ceiling and wall lists own their geometry; the south wall also owns its window.",
            60,
            330);
        GraphNode systemsNote = graph.Note(
            802,
            "Plant components feed terminal systems; terminal and ERV outputs connect directly to the zone.",
            60,
            1180);
        GraphNode modelNote = graph.Note(
            803,
            "The completed zone and optional photovoltaic system form the energy model.",
            1200,
            940);
        GraphNode compileNote = graph.Note(
            804,
            "Compile the model internally to an IDF and inspect validation before running.",
            1810,
            250);
        GraphNode weatherNote = graph.Note(
            805,
            "InvisibleDragon accepts the user-selected EPW and verifies it before simulation.",
            1810,
            1300);
        GraphNode runNote = graph.Note(
            806,
            "Set Run to True for a rising edge. Cancel, force-rerun and timeout remain optional controls.",
            2460,
            690);
        graph.Group(
            900,
            "1  Envelope construction",
            ExampleGroupTheme.Inputs,
            constructionNote,
            material,
            thickness,
            layer,
            construction);
        graph.Group(
            901,
            "2  Zone envelope, opening and profile",
            ExampleGroupTheme.Envelope,
            new[]
            {
                envelopeNote,
                profile,
                glazing,
                windowBoundary,
                window,
                floorName,
                ceilingName,
                plainWallNames,
                southWallName,
                groundBoundary,
            }
                .Concat(curves)
                .Concat(surfaces)
                .ToArray());
        graph.Group(
            902,
            "3  Zone systems",
            ExampleGroupTheme.Systems,
            systemsNote,
            heatPump,
            airHandler,
            boiler,
            radiantFloor,
            ventilationFlow,
            ventilator);
        graph.Group(
            903,
            "4  Zone and energy model",
            ExampleGroupTheme.Model,
            modelNote,
            zone,
            photovoltaic,
            model);
        graph.Group(
            904,
            "5  Managed IDF compile",
            ExampleGroupTheme.Model,
            compileNote,
            compile,
            idfText,
            valid,
            diagnostics);
        graph.Group(
            905,
            "6  Weather",
            ExampleGroupTheme.Inputs,
            weatherNote,
            epwPath,
            weather,
            weatherSuccess,
            weatherDiagnostics);
        graph.Group(
            906,
            "7  Managed EnergyPlus run",
            ExampleGroupTheme.Runtime,
            runNote,
            runTrigger,
            cancelTrigger,
            forceRerun,
            timeout,
            run,
            result,
            runState,
            runSuccess,
            runDiagnostics);
        return graph.Build(
            InvisibleProduct,
            "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonIdfGoo",
            envelope: new OutwardEnvelopeExpectation(
                curves.Select(item => item.InstanceGuid).ToArray(),
                new Point3d(4, 3, 1.5)),
            runtimeWorkflow: new InvisibleRuntimeWorkflowExpectation(
                compile.InstanceGuid,
                epwPath.InstanceGuid,
                weather.InstanceGuid,
                run.InstanceGuid,
                runTrigger.InstanceGuid,
                cancelTrigger.InstanceGuid,
                forceRerun.InstanceGuid));
    }

    private static ScenarioGraph BuildSimpleEnvelopeHvac(GH_ComponentServer server)
    {
        var graph = new ScenarioGraphBuilder(server, "21000000");
        string[] materialNames = { "Exterior Concrete", "Mineral Wool", "Gypsum Board" };
        decimal[] values = { 0.2m, 0.12m, 0.013m };
        GraphNode[] names = new GraphNode[3];
        GraphNode[] materials = new GraphNode[3];
        GraphNode[] thicknesses = new GraphNode[3];
        for (int index = 0; index < 3; index++)
        {
            names[index] = graph.Panel(1 + index, materialNames[index] + " name", materialNames[index], 60, 80 + (index * 150));
            materials[index] = graph.Component(10 + index, Catalog.SimpleMaterial, 300, 60 + (index * 150));
            thicknesses[index] = graph.Slider(20 + index, materialNames[index] + " thickness", values[index], 0.001m, 0.5m, 320, 130 + (index * 150));
            graph.Connect(names[index], null, materials[index], 0);
        }

        GraphNode construction = graph.Component(30, Catalog.SimpleConstruction, 860, 230);
        GraphNode[] layers = new GraphNode[3];
        for (int index = 0; index < layers.Length; index++)
        {
            layers[index] = graph.Component(23 + index, Catalog.SimpleLayer, 580, 60 + (index * 150));
            graph.Connect(materials[index], 0, layers[index], 0);
            graph.Connect(thicknesses[index], null, layers[index], 1);
            graph.Connect(layers[index], 0, construction, 1);
            graph.ExpectOutput(
                layers[index],
                0,
                1,
                "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonSurfaceConstructionLayerGoo");
        }

        GraphNode fenestration = graph.Component(31, Catalog.SimpleFenestration, 860, 520);
        GraphNode profileName = graph.Panel(32, "Packaged office profile", "\uC18C\uADDC\uBAA8\uC0AC\uBB34\uC2E4", 680, 730);
        GraphNode profile = graph.Component(33, Catalog.SimpleProfile, 980, 700);
        graph.Connect(profileName, null, profile, 0);
        GraphNode heatPump = graph.Component(40, Catalog.SimpleHeatPump, 60, 1000);
        GraphNode airHandler = graph.Component(41, Catalog.SimpleAirHandler, 360, 1000);
        GraphNode boiler = graph.Component(42, Catalog.SimpleBoiler, 60, 1190);
        GraphNode radiator = graph.Component(43, Catalog.SimpleRadiator, 360, 1190);
        GraphNode chiller = graph.Component(44, Catalog.SimpleChiller, 60, 1380);
        GraphNode fanCoil = graph.Component(45, Catalog.SimpleFanCoil, 360, 1380);
        GraphNode ventilator = graph.Component(46, Catalog.SimpleErv, 60, 1580);
        GraphNode photovoltaic = graph.Component(47, Catalog.SimplePv, 360, 1580);
        graph.Connect(heatPump, 0, airHandler, 1);
        graph.Connect(boiler, 0, radiator, 1);
        graph.Connect(chiller, 0, fanCoil, 1);
        GraphNode uValue = graph.Panel(50, "Envelope U-value", string.Empty, 1040, 250);
        GraphNode profileValue = graph.Panel(51, "Resolved profile", string.Empty, 1280, 720);
        GraphNode systems = graph.Panel(52, "HVAC families", string.Empty, 1040, 1150);
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
        graph.ExpectOutput(ventilator, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonZoneErvGoo");
        graph.ExpectOutput(photovoltaic, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonPhotovoltaicPanelGoo");
        graph.ExpectOutput(systems, null, 3);
        GraphNode materialNote = graph.Note(
            800,
            "Create reusable materials, pair each with a thickness, then collect the layer outputs.",
            60,
            -60);
        GraphNode envelopeNote = graph.Note(
            801,
            "Opaque and fenestration constructions are independent envelope resources.",
            820,
            120);
        GraphNode profileNote = graph.Note(
            802,
            "The packaged office profile resolves occupancy and operational schedules.",
            300,
            650);
        GraphNode systemsNote = graph.Note(
            803,
            "Compare plant-to-terminal HVAC families; ERV and photovoltaic definitions remain optional.",
            60,
            880);
        graph.Group(
            900,
            "1  Materials and layers",
            ExampleGroupTheme.Inputs,
            new[] { materialNote }
                .Concat(names)
                .Concat(materials)
                .Concat(thicknesses)
                .Concat(layers)
                .ToArray());
        graph.Group(
            901,
            "2  Envelope constructions",
            ExampleGroupTheme.Envelope,
            envelopeNote,
            construction,
            fenestration,
            uValue);
        graph.Group(
            902,
            "3  Usage profile",
            ExampleGroupTheme.Model,
            profileNote,
            profileName,
            profile,
            profileValue);
        graph.Group(
            903,
            "4  HVAC, ERV and PV families",
            ExampleGroupTheme.Systems,
            systemsNote,
            heatPump,
            airHandler,
            boiler,
            radiator,
            chiller,
            fanCoil,
            ventilator,
            photovoltaic,
            systems);
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
        ExampleSurfaceGeometry[] surfaceGeometry = ExampleBuildingModels.CreateSurfaceBreps(TwoZoneModel);
        Curve[] openingGeometry = ExampleBuildingModels.CreateOpeningCurves(TwoZoneModel);
        Require(surfaceGeometry.Length == 12, TwoZoneModel + " must provide exactly twelve Zone-owned Surface Breps.");
        Require(openingGeometry.Length == 2, TwoZoneModel + " must provide exactly two opening curves.");
        GraphNode modelInfo = graph.Note(
            1,
            "Open 30-two-zone-office.3dm; relink each named face Brep and window to its matching local Surface cluster.",
            40,
            -60);
        GraphNode westCurve = graph.Curves(4, "WINDOW_ZONE_01_SOUTH", new[] { openingGeometry[0] }, 60, 760);
        GraphNode eastCurve = graph.Curves(5, "WINDOW_ZONE_02_SOUTH", new[] { openingGeometry[1] }, 60, 2170);
        GraphNode profileName = graph.Panel(6, "Packaged office profile", "\uC18C\uADDC\uBAA8\uC0AC\uBB34\uC2E4", 780, -80);
        GraphNode profile = graph.Component(7, Catalog.SimpleProfile, 1080, -80);
        GraphNode material = graph.Component(8, Catalog.SimpleMaterial, 330, 80);
        GraphNode thickness = graph.Slider(9, "Envelope 0.200 m", 0.2m, 0.01m, 0.5m, 350, 220);
        GraphNode layer = graph.Component(14, Catalog.SimpleLayer, 620, 100);
        GraphNode construction = graph.Component(10, Catalog.SimpleConstruction, 730, 100);
        GraphNode fenestration = graph.Component(11, Catalog.SimpleFenestration, 850, 180);
        GraphNode westOpening = graph.Component(12, Catalog.SimpleOpening, 950, 750);
        GraphNode eastOpening = graph.Component(13, Catalog.SimpleOpening, 950, 2160);
        graph.Connect(profileName, null, profile, 0);
        graph.Connect(material, 0, layer, 0);
        graph.Connect(thickness, null, layer, 1);
        graph.Connect(layer, 0, construction, 1);
        graph.Connect(westCurve, null, westOpening, 0);
        graph.Connect(eastCurve, null, eastOpening, 0);
        graph.Connect(fenestration, 0, westOpening, 3);
        graph.Connect(fenestration, 0, eastOpening, 3);

        (GraphNode[] westSurfaces, GraphNode[] westFaceParameters) = BuildSimpleSurfaceCluster(
            graph,
            surfaceGeometry,
            "ZONE_01_WEST",
            construction,
            westOpening,
            westCurve,
            keyBase: 200,
            yStart: 390);
        (GraphNode[] eastSurfaces, GraphNode[] eastFaceParameters) = BuildSimpleSurfaceCluster(
            graph,
            surfaceGeometry,
            "ZONE_02_EAST",
            construction,
            eastOpening,
            eastCurve,
            keyBase: 300,
            yStart: 1800);

        GraphNode westHvac;
        GraphNode eastHvac;
        GraphNode? westPlant = null;
        GraphNode? eastPlant = null;
        GraphNode westVentilator = graph.Component(25, Catalog.SimpleErv, 1250, 1560);
        GraphNode eastVentilator = graph.Component(26, Catalog.SimpleErv, 1250, 2950);
        GraphNode? photovoltaic = null;
        if (!includeRuntimeWorkflow)
        {
            GraphNode heatPump = graph.Component(20, Catalog.SimpleHeatPump, 680, 1460);
            westHvac = graph.Component(21, Catalog.SimpleAirHandler, 1080, 1460);
            GraphNode boiler = graph.Component(22, Catalog.SimpleBoiler, 680, 2850);
            eastHvac = graph.Component(23, Catalog.SimpleRadiator, 1080, 2850);
            photovoltaic = graph.Component(27, Catalog.SimplePv, 1470, 2150);
            westPlant = heatPump;
            eastPlant = boiler;
            graph.Connect(heatPump, 0, westHvac, 1);
            graph.Connect(boiler, 0, eastHvac, 1);
        }
        else
        {
            westHvac = graph.Component(21, Catalog.SimpleElectricRadiator, 1080, 1460);
            eastHvac = graph.Component(23, Catalog.SimpleElectricRadiator, 1080, 2850);
        }

        GraphNode westHvacName = graph.Panel(15, "West Zone HVAC", "West Zone HVAC", 780, 1460);
        GraphNode eastHvacName = graph.Panel(16, "East Zone HVAC", "East Zone HVAC", 780, 2850);
        GraphNode westErvName = graph.Panel(17, "West Zone ERV", "West Zone ERV", 780, 1560);
        GraphNode eastErvName = graph.Panel(18, "East Zone ERV", "East Zone ERV", 780, 2950);
        graph.Connect(westHvacName, null, westHvac, 0);
        graph.Connect(eastHvacName, null, eastHvac, 0);
        graph.Connect(westErvName, null, westVentilator, 0);
        graph.Connect(eastErvName, null, eastVentilator, 0);

        GraphNode westZone = graph.Component(30, Catalog.SimpleZone, 1400, 1350);
        GraphNode eastZone = graph.Component(31, Catalog.SimpleZone, 1400, 2740);
        GraphNode westZoneName = graph.Panel(180, "West Zone name", "West Office Zone", 990, 1270);
        GraphNode eastZoneName = graph.Panel(181, "East Zone name", "East Office Zone", 990, 2660);
        GraphNode westHeight = graph.Slider(182, "West Zone height 3.200 m", 3.2m, 0.1m, 20m, 300, 1390);
        GraphNode eastHeight = graph.Slider(183, "East Zone height 3.200 m", 3.2m, 0.1m, 20m, 300, 2780);
        foreach (GraphNode surface in westSurfaces)
        {
            graph.Connect(surface, 0, westZone, 0);
        }

        foreach (GraphNode surface in eastSurfaces)
        {
            graph.Connect(surface, 0, eastZone, 0);
        }

        graph.Connect(westZoneName, null, westZone, 1);
        graph.Connect(eastZoneName, null, eastZone, 1);
        graph.Connect(westHeight, null, westZone, 3);
        graph.Connect(eastHeight, null, eastZone, 3);
        graph.Connect(profile, 0, westZone, 4);
        graph.Connect(profile, 0, eastZone, 4);
        graph.Connect(westHvac, 0, westZone, 5);
        graph.Connect(eastHvac, 0, eastZone, 5);
        graph.Connect(westVentilator, 0, westZone, 6);
        graph.Connect(eastVentilator, 0, eastZone, 6);

        GraphNode modelName = graph.Panel(28, "Model name", "Two-Zone Office", 1470, 1960);
        GraphNode model = graph.Component(32, Catalog.SimpleModel, 1770, 2000);
        graph.Connect(modelName, null, model, 0);
        graph.Connect(westZone, 0, model, 1);
        graph.Connect(eastZone, 0, model, 1);
        if (photovoltaic is not null)
        {
            graph.Connect(photovoltaic, 0, model, 6);
        }
        GraphNode map = graph.Panel(40, "Geometry provenance map", string.Empty, 2000, 1880);
        GraphNode area = graph.Panel(41, "Total floor area", string.Empty, 2000, 2010);
        graph.Connect(model, 3, map, null);
        graph.Connect(model, 6, area, null);
        graph.ExpectOutput(westOpening, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonOpeningDefinitionGoo");
        graph.ExpectOutput(eastOpening, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonOpeningDefinitionGoo");
        foreach (GraphNode surface in westSurfaces.Concat(eastSurfaces))
        {
            graph.ExpectOutput(
                surface,
                0,
                1,
                "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonSurfaceDefinitionGoo");
        }
        graph.ExpectOutput(
            layer,
            0,
            1,
            "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonSurfaceConstructionLayerGoo");
        graph.ExpectOutput(westZone, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonZoneDefinitionGoo");
        graph.ExpectOutput(eastZone, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonZoneDefinitionGoo");
        graph.ExpectOutput(model, 0, 1, "GonieGonie.SimpleDragon.Grasshopper.Types.GreenRetrofitModelGoo");
        graph.ExpectOutput(model, 1, 2, "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonZoneGoo");
        graph.ExpectOutput(map, null, 1);
        graph.ExpectOutput(area, null, 1);
        graph.ExpectNumber(model, 6, 96, 1e-8);

        GraphNode sharedNote = graph.Note(
            800,
            "Shared construction, fenestration and office-profile definitions feed both zone lanes.",
            1300,
            10);
        GraphNode westSystemsNote = graph.Note(
            801,
            "West zone: attach HVAC and ERV directly after collecting the west surface list.",
            660,
            1190);
        GraphNode eastSystemsNote = graph.Note(
            802,
            "East zone: the parallel lane repeats the same ownership pattern without index matching.",
            660,
            2600);
        graph.Group(
            900,
            "1  Rhino source and shared definitions",
            ExampleGroupTheme.Inputs,
            modelInfo,
            sharedNote,
            profileName,
            profile,
            material,
            thickness,
            layer,
            construction,
            fenestration);
        var westSystemMembers = new List<GraphNode>
        {
            westSystemsNote,
            westHvacName,
            westHvac,
            westErvName,
            westVentilator,
            westZoneName,
            westHeight,
            westZone,
        };
        var eastSystemMembers = new List<GraphNode>
        {
            eastSystemsNote,
            eastHvacName,
            eastHvac,
            eastErvName,
            eastVentilator,
            eastZoneName,
            eastHeight,
            eastZone,
        };
        if (westPlant is not null)
        {
            westSystemMembers.Add(westPlant);
        }

        if (eastPlant is not null)
        {
            eastSystemMembers.Add(eastPlant);
        }

        graph.Group(
            901,
            "3  West systems and zone",
            ExampleGroupTheme.Systems,
            westSystemMembers.ToArray());
        graph.Group(
            902,
            "5  East systems and zone",
            ExampleGroupTheme.Systems,
            eastSystemMembers.ToArray());

        RuntimeWorkflowExpectation? runtimeWorkflow = null;
        if (!includeRuntimeWorkflow)
        {
            GraphNode json = graph.Panel(42, "Complete GRM JSON", string.Empty, 2230, 1880);
            GraphNode diagnostics = graph.Panel(43, "Model diagnostics", string.Empty, 2230, 2050);
            graph.Connect(model, 5, json, null);
            graph.Connect(model, 7, diagnostics, null);
            graph.ExpectOutput(json, null, 1);
            graph.ExpectOutput(diagnostics, null, 1);
            GraphNode modelNote = graph.Note(
                803,
                "Collect both zone objects into one model; inspect area, provenance, GRM JSON and diagnostics.",
                1450,
                1810);
            graph.Group(
                903,
                "6  Two-zone model and inspection",
                ExampleGroupTheme.Model,
                new[]
                {
                    modelNote,
                    modelName,
                    model,
                    map,
                    area,
                    json,
                    diagnostics,
                }
                    .Concat(photovoltaic is null ? Array.Empty<GraphNode>() : new[] { photovoltaic! })
                    .ToArray());
        }
        else
        {
            GraphNode modelNote = graph.Note(
                803,
                "The two completed zones form the model passed directly into SimpleDragon Run.",
                1450,
                1810);
            graph.Group(
                903,
                "6  Two-zone model",
                ExampleGroupTheme.Model,
                modelNote,
                modelName,
                model,
                map,
                area);

            GraphNode runTrigger = graph.Boolean(103, "Run - explicit rising edge", false, 2500, 1700);
            GraphNode cancelTrigger = graph.Boolean(104, "Cancel active run", false, 2500, 1780);
            GraphNode forceRerun = graph.Boolean(105, "Force rerun", false, 2500, 1860);
            GraphNode timeout = graph.Slider(107, "Run timeout 2 min", 2m, 1m, 30m, 2500, 1940);
            GraphNode run = graph.Component(110, Catalog.SimpleRun, 2850, 1780);
            graph.Connect(model, 0, run, 0);
            graph.Connect(runTrigger, null, run, 1);
            graph.Connect(cancelTrigger, null, run, 2);
            graph.Connect(forceRerun, null, run, 3);
            graph.Connect(timeout, null, run, 4);

            GraphNode resultSummary = graph.Component(113, Catalog.SimpleResultSummary, 3200, 1180);
            GraphNode monthlyLines = graph.Component(119, Catalog.SimpleLinePlot, 3200, 1390);
            GraphNode exportDirectory = graph.Panel(
                114,
                "CSV export directory",
                @"..\temp\example-preview\run-results-csv",
                3200,
                2520);
            GraphNode exportTrigger = graph.Boolean(116, "Export CSV", false, 3200, 2700);
            GraphNode overwrite = graph.Boolean(117, "Overwrite CSV", false, 3200, 2780);
            GraphNode exportCsv = graph.Component(118, Catalog.SimpleExportCsv, 3550, 2550);
            graph.Connect(run, 0, resultSummary, 0);
            graph.Connect(run, 0, monthlyLines, 0);
            graph.Connect(run, 0, exportCsv, 0);
            graph.Connect(model, 0, exportCsv, 1);
            graph.Connect(exportDirectory, null, exportCsv, 2);
            graph.Connect(run, 3, exportCsv, 3);
            graph.Connect(model, 4, exportCsv, 4);
            graph.Connect(exportTrigger, null, exportCsv, 5);
            graph.Connect(overwrite, null, exportCsv, 6);
            GraphNode runState = graph.Panel(130, "SimpleDragon run state", string.Empty, 3200, 1740);
            GraphNode runSuccess = graph.Panel(131, "SimpleDragon run success", string.Empty, 3200, 1900);
            GraphNode annualResult = graph.Panel(134, "Annual site result", string.Empty, 3550, 1210);
            GraphNode csvFiles = graph.Panel(135, "CSV package files", string.Empty, 3920, 2550);
            GraphNode csvWritten = graph.Panel(136, "CSV written", string.Empty, 3920, 2750);
            graph.Connect(run, 1, runState, null);
            graph.Connect(run, 2, runSuccess, null);
            graph.Connect(resultSummary, 1, annualResult, null);
            graph.Connect(exportCsv, 2, csvFiles, null);
            graph.Connect(exportCsv, 4, csvWritten, null);
            graph.ExpectOutput(run, 1, 1);
            graph.ExpectOutput(run, 2, 1);
            graph.ExpectOutput(runState, null, 1);
            graph.ExpectOutput(runSuccess, null, 1);
            graph.ExpectBoolean(run, 2, false);

            GraphNode batchCase = graph.Component(141, Catalog.SimpleBatchCase, 2850, 3020);
            GraphNode batchParallel = graph.Slider(142, "Batch parallel limit", 1m, 1m, 16m, 2850, 3170);
            GraphNode batchRun = graph.Boolean(143, "Run batch", false, 2850, 3250);
            GraphNode batchCancel = graph.Boolean(144, "Cancel batch", false, 2850, 3330);
            GraphNode managedBatch = graph.Component(145, Catalog.SimpleManagedBatch, 3200, 3110);
            GraphNode batchState = graph.Panel(146, "Managed batch state", string.Empty, 3550, 3070);
            GraphNode batchComplete = graph.Panel(147, "Managed batch complete", string.Empty, 3550, 3230);
            graph.Connect(model, 0, batchCase, 0);
            graph.Connect(batchCase, 0, managedBatch, 0);
            graph.Connect(batchParallel, null, managedBatch, 1);
            graph.Connect(batchRun, null, managedBatch, 2);
            graph.Connect(batchCancel, null, managedBatch, 3);
            graph.Connect(managedBatch, 0, batchState, null);
            graph.Connect(managedBatch, 5, batchComplete, null);
            graph.ExpectOutput(
                batchCase,
                0,
                1,
                "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonBatchCaseGoo");
            graph.ExpectOutput(managedBatch, 0, 1);
            graph.ExpectOutput(batchState, null, 1);
            graph.ExpectOutput(batchComplete, null, 1);
            graph.ExpectBoolean(managedBatch, 5, false);

            GraphNode runNote = graph.Note(
                804,
                "SimpleDragon resolves EnergyPlus and weather internally; toggle Run for a rising edge.",
                2480,
                1630);
            GraphNode resultsNote = graph.Note(
                805,
                "Connect the Run result directly for annual summary and default monthly line geometry.",
                3180,
                1020);
            GraphNode csvNote = graph.Note(
                806,
                "CSV export is optional; choose a temporary directory and toggle Export CSV.",
                3180,
                2440);
            GraphNode batchNote = graph.Note(
                807,
                "The same model can become a managed batch case with explicit run and cancel controls.",
                2830,
                2940);
            graph.Group(
                904,
                "7  Single simulation",
                ExampleGroupTheme.Runtime,
                runNote,
                runTrigger,
                cancelTrigger,
                forceRerun,
                timeout,
                run,
                runState,
                runSuccess);
            graph.Group(
                905,
                "8  Results and monthly graph",
                ExampleGroupTheme.Results,
                resultsNote,
                resultSummary,
                monthlyLines,
                annualResult);
            graph.Group(
                906,
                "9  Optional CSV package",
                ExampleGroupTheme.Results,
                csvNote,
                exportDirectory,
                exportTrigger,
                overwrite,
                exportCsv,
                csvFiles,
                csvWritten);
            graph.Group(
                907,
                "10  Managed batch",
                ExampleGroupTheme.Runtime,
                batchNote,
                batchCase,
                batchParallel,
                batchRun,
                batchCancel,
                managedBatch,
                batchState,
                batchComplete);

            runtimeWorkflow = new SimpleRuntimeWorkflowExpectation(
                run.InstanceGuid,
                runTrigger.InstanceGuid,
                cancelTrigger.InstanceGuid,
                forceRerun.InstanceGuid,
                resultSummary.InstanceGuid,
                monthlyLines.InstanceGuid,
                exportCsv.InstanceGuid,
                exportDirectory.InstanceGuid,
                exportTrigger.InstanceGuid,
                overwrite.InstanceGuid,
                managedBatch.InstanceGuid,
                modelName.InstanceGuid,
                batchRun.InstanceGuid,
                batchCancel.InstanceGuid);
        }

        return graph.Build(
            SimpleProduct,
            includeRuntimeWorkflow
                ? "GonieGonie.SimpleDragon.Grasshopper.Types.GreenRetrofitResultGoo"
                : "GonieGonie.SimpleDragon.Grasshopper.Types.GreenRetrofitModelGoo",
            new LinkedModelExpectation(
                TwoZoneModel,
                westFaceParameters.Concat(eastFaceParameters).Select(item => item.InstanceGuid).ToArray(),
                new[] { westCurve.InstanceGuid, eastCurve.InstanceGuid }),
            runtimeWorkflow: runtimeWorkflow);
    }

    private static (GraphNode[] Surfaces, GraphNode[] FaceParameters) BuildSimpleSurfaceCluster(
        ScenarioGraphBuilder graph,
        IReadOnlyList<ExampleSurfaceGeometry> allGeometry,
        string zoneName,
        GraphNode construction,
        GraphNode opening,
        GraphNode openingSource,
        int keyBase,
        float yStart)
    {
        ExampleSurfaceGeometry[] definitions = allGeometry
            .Where(item => string.Equals(item.ZoneName, zoneName, StringComparison.Ordinal))
            .ToArray();
        Require(definitions.Length == 6, zoneName + " must provide exactly six named Surface Breps.");
        ExampleSurfaceGeometry floorDefinition = definitions.Single(item => item.Type == ExampleSurfaceType.Floor);
        ExampleSurfaceGeometry ceilingDefinition = definitions.Single(item => item.Type == ExampleSurfaceType.Ceiling);
        ExampleSurfaceGeometry southWallDefinition = definitions.Single(item =>
            item.Type == ExampleSurfaceType.Wall
            && item.Name.EndsWith("_SOUTH", StringComparison.Ordinal));
        ExampleSurfaceGeometry[] plainWallDefinitions = definitions
            .Where(item => item.Type == ExampleSurfaceType.Wall && !ReferenceEquals(item, southWallDefinition))
            .ToArray();
        Require(plainWallDefinitions.Length == 3, zoneName + " must provide exactly three opening-free Wall Breps.");
        Require(
            string.Equals(floorDefinition.BoundaryIntent, "Ground", StringComparison.Ordinal),
            zoneName + " example Floor must use the Ground boundary selector.");

        GraphNode floorFace = graph.Breps(keyBase, floorDefinition.Name, new[] { floorDefinition.Geometry }, 60, yStart);
        GraphNode floorName = graph.Panel(keyBase + 1, floorDefinition.Name + " name", floorDefinition.Name, 300, yStart);
        GraphNode floorBoundary = graph.ValueList(
            keyBase + 2,
            floorDefinition.Name + " boundary",
            SurfaceBoundaryChoices,
            "Ground",
            760,
            yStart + 60);
        GraphNode floor = graph.Component(keyBase + 3, Catalog.SimpleFloor, 1150, yStart + 10);

        GraphNode ceilingFace = graph.Breps(
            keyBase + 10,
            ceilingDefinition.Name,
            new[] { ceilingDefinition.Geometry },
            60,
            yStart + 220);
        GraphNode ceilingName = graph.Panel(
            keyBase + 11,
            ceilingDefinition.Name + " name",
            ceilingDefinition.Name,
            300,
            yStart + 220);
        GraphNode ceiling = graph.Component(keyBase + 13, Catalog.SimpleCeiling, 1150, yStart + 230);

        GraphNode plainWallFaces = graph.Breps(
            keyBase + 20,
            zoneName + " plain walls (list)",
            plainWallDefinitions.Select(item => item.Geometry),
            60,
            yStart + 440);
        GraphNode plainWallNames = graph.Strings(
            keyBase + 21,
            zoneName + " plain wall names (list)",
            plainWallDefinitions.Select(item => item.Name),
            300,
            yStart + 440);
        GraphNode plainWalls = graph.Component(keyBase + 23, Catalog.SimpleWall, 1150, yStart + 450);

        GraphNode southWallFace = graph.Breps(
            keyBase + 30,
            southWallDefinition.Name,
            new[] { southWallDefinition.Geometry },
            60,
            yStart + 660);
        GraphNode southWallName = graph.Panel(
            keyBase + 31,
            southWallDefinition.Name + " name",
            southWallDefinition.Name,
            300,
            yStart + 660);
        GraphNode southWall = graph.Component(keyBase + 33, Catalog.SimpleWall, 1150, yStart + 670);

        graph.Connect(floorFace, null, floor, 0);
        graph.Connect(floorName, null, floor, 1);
        graph.Connect(construction, 0, floor, 2);
        graph.Connect(floorBoundary, null, floor, 3);
        graph.Connect(ceilingFace, null, ceiling, 0);
        graph.Connect(ceilingName, null, ceiling, 1);
        graph.Connect(construction, 0, ceiling, 2);
        graph.Connect(plainWallFaces, null, plainWalls, 0);
        graph.Connect(plainWallNames, null, plainWalls, 1);
        graph.Connect(construction, 0, plainWalls, 2);
        graph.Connect(southWallFace, null, southWall, 0);
        graph.Connect(southWallName, null, southWall, 1);
        graph.Connect(construction, 0, southWall, 2);
        graph.Connect(opening, 0, southWall, 4);

        string zoneLabel = zoneName == "ZONE_01_WEST" ? "West" : "East";
        GraphNode note = graph.Note(
            keyBase + 90,
            zoneLabel + " envelope: lists author repeated walls; the south wall owns its opening.",
            40,
            yStart - 70);
        graph.Group(
            keyBase + 91,
            (zoneLabel == "West" ? "2  " : "4  ") + zoneLabel + " zone envelope",
            ExampleGroupTheme.Envelope,
            note,
            openingSource,
            opening,
            floorFace,
            floorName,
            floorBoundary,
            floor,
            ceilingFace,
            ceilingName,
            ceiling,
            plainWallFaces,
            plainWallNames,
            plainWalls,
            southWallFace,
            southWallName,
            southWall);

        return (
            new[] { floor, ceiling, plainWalls, southWall },
            new[] { floorFace, ceilingFace, plainWallFaces, southWallFace });
    }

    private static ScenarioGraph BuildSimpleResultsAndPlots(GH_ComponentServer server)
    {
        var graph = new ScenarioGraphBuilder(server, "23000000");
        GraphNode resultPath = graph.FilePath(
            1,
            "GRR fixture path",
            @"..\fixtures\simple-dragon\grr\ASHRAE 140 modified.grr",
            60,
            500);
        GraphNode read = graph.Component(2, Catalog.SimpleReadResult, 390, 490);
        GraphNode summary = graph.Component(3, Catalog.SimpleResultSummary, 760, 40);
        GraphNode dataTree = graph.Component(4, Catalog.SimpleDataTree, 760, 300);
        GraphNode linePlot = graph.Component(5, Catalog.SimpleLinePlot, 760, 680);
        GraphNode barPlot = graph.Component(6, Catalog.SimpleBarPlot, 760, 960);
        GraphNode export = graph.Component(7, Catalog.SimpleExportCsv, 760, 1280);
        GraphNode exportDirectory = graph.Panel(
            8,
            "Preview export directory",
            @"..\temp\example-preview\simpledragon-csv",
            390,
            1330);
        GraphNode annual = graph.Panel(10, "Annual site use", string.Empty, 1160, 70);
        GraphNode monthly = graph.Panel(11, "Monthly data tree", string.Empty, 1160, 330);
        GraphNode lines = graph.Panel(12, "Line plot curves", string.Empty, 1160, 710);
        GraphNode bars = graph.Panel(13, "Bar plot curves", string.Empty, 1160, 990);
        GraphNode csvFiles = graph.Panel(14, "CSV preview files", string.Empty, 1160, 1310);
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
        GraphNode sourceNote = graph.Note(
            800,
            "Read the bundled GRR fixture once, then fan the typed result out to parallel consumers.",
            40,
            420);
        GraphNode summaryNote = graph.Note(
            801,
            "Summary and data-tree components expose annual and monthly numeric results.",
            730,
            -120);
        GraphNode plotNote = graph.Note(
            802,
            "Line and bar components create preview geometry from the same result object.",
            730,
            500);
        GraphNode csvNote = graph.Note(
            803,
            "CSV export writes only when explicitly triggered; this example previews the target package.",
            360,
            1160);
        graph.Group(
            900,
            "1  Result source",
            ExampleGroupTheme.Inputs,
            sourceNote,
            resultPath,
            read);
        graph.Group(
            901,
            "2  Numeric summaries",
            ExampleGroupTheme.Results,
            summaryNote,
            summary,
            dataTree,
            annual,
            monthly);
        graph.Group(
            902,
            "3  Preview graphs",
            ExampleGroupTheme.Results,
            plotNote,
            linePlot,
            barPlot,
            lines,
            bars);
        graph.Group(
            903,
            "4  Optional CSV export",
            ExampleGroupTheme.Results,
            csvNote,
            exportDirectory,
            export,
            csvFiles);
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
            if (actual is not GH_Group)
            {
                Require(
                    Math.Abs(actual.Attributes.Pivot.X - expected.Pivot.X) <= 0.1f
                        && Math.Abs(actual.Attributes.Pivot.Y - expected.Pivot.Y) <= 0.1f,
                    expected.InstanceGuid + " canvas position changed: expected " + expected.Pivot
                        + " but reopened at " + actual.Attributes.Pivot + ".");
            }

            if (expected.ComponentGuid.HasValue)
            {
                Require(actual is GH_Component, expected.InstanceGuid + " must remain a Grasshopper component.");
                Require(
                    ((GH_Component)actual).ComponentGuid == expected.ComponentGuid.Value,
                    expected.InstanceGuid + " component identity changed.");
            }
        }

        foreach (NoteExpectation expected in graph.Notes)
        {
            GH_Scribble note = RequireObject<GH_Scribble>(document, expected.InstanceGuid);
            Require(
                string.Equals(note.Text, expected.Text, StringComparison.Ordinal),
                expected.InstanceGuid + " canvas note text changed.");
            Require(
                string.Equals(note.Font.Name, GH_FontServer.Large.Name, StringComparison.Ordinal)
                    && Math.Abs(note.Font.Size - GH_FontServer.Large.Size) <= 0.1f
                    && note.Font.Style == GH_FontServer.Large.Style,
                expected.InstanceGuid + " canvas note font changed.");
            Require(
                note.Attributes.Bounds.Width > 20 && note.Attributes.Bounds.Height > 10,
                expected.InstanceGuid + " canvas note has invalid display bounds.");
        }

        foreach (GroupExpectation expected in graph.Groups)
        {
            GH_Group group = RequireObject<GH_Group>(document, expected.InstanceGuid);
            Require(
                string.Equals(group.NickName, expected.Name, StringComparison.Ordinal),
                expected.InstanceGuid + " canvas group name changed.");
            Require(
                group.Border == expected.Border && group.Colour.ToArgb() == expected.ColourArgb,
                expected.InstanceGuid + " canvas group appearance changed.");
            Require(
                group.ObjectIDs.Count == expected.MemberGuids.Length
                    && group.ObjectIDs.ToHashSet().SetEquals(expected.MemberGuids),
                expected.InstanceGuid + " canvas group membership changed.");
            Require(
                group.Attributes.Bounds.Width > 20 && group.Attributes.Bounds.Height > 20,
                expected.InstanceGuid + " canvas group has invalid display bounds.");
            IGH_DocumentObject[] memberObjects = expected.MemberGuids
                .Select(memberGuid => document.FindObject(memberGuid, topLevelOnly: true)
                    ?? throw new InvalidOperationException("Canvas group member is absent."))
                .ToArray();
            for (int first = 0; first < memberObjects.Length; first++)
            {
                for (int second = first + 1; second < memberObjects.Length; second++)
                {
                    System.Drawing.RectangleF intersection = System.Drawing.RectangleF.Intersect(
                        memberObjects[first].Attributes.Bounds,
                        memberObjects[second].Attributes.Bounds);
                    Require(
                        intersection.Width <= 1 || intersection.Height <= 1,
                        "Objects " + memberObjects[first].InstanceGuid + " and "
                            + memberObjects[second].InstanceGuid + " overlap inside canvas group '"
                            + group.NickName + "': " + memberObjects[first].Attributes.Bounds + " vs "
                            + memberObjects[second].Attributes.Bounds + ".");
                }
            }
        }

        GH_Group[] displayedGroups = graph.Groups
            .Select(expected => RequireObject<GH_Group>(document, expected.InstanceGuid))
            .ToArray();
        for (int first = 0; first < displayedGroups.Length; first++)
        {
            for (int second = first + 1; second < displayedGroups.Length; second++)
            {
                System.Drawing.RectangleF intersection = System.Drawing.RectangleF.Intersect(
                    displayedGroups[first].Attributes.Bounds,
                    displayedGroups[second].Attributes.Bounds);
                Require(
                    intersection.Width <= 1 || intersection.Height <= 1,
                    "Canvas groups '" + displayedGroups[first].NickName + "' and '"
                        + displayedGroups[second].NickName + "' overlap: "
                        + displayedGroups[first].Attributes.Bounds + " vs "
                        + displayedGroups[second].Attributes.Bounds + ".");
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
                Require(
                    string.Equals(source.Name, expected.SourceParameterName, StringComparison.Ordinal)
                        && string.Equals(source.GetType().FullName, expected.SourceParameterType, StringComparison.Ordinal)
                        && source.Access == expected.SourceAccess,
                    "Wire source contract changed for " + expected.SourceObjectGuid + ".");
                Require(
                    string.Equals(target.Name, expected.TargetParameterName, StringComparison.Ordinal)
                        && string.Equals(target.GetType().FullName, expected.TargetParameterType, StringComparison.Ordinal)
                        && target.Access == expected.TargetAccess,
                    "Wire target contract changed for " + targetGroup.Key.ObjectGuid + ".");
                IGH_DocumentObject sourceObject = document.FindObject(
                    expected.SourceObjectGuid,
                    topLevelOnly: true)
                    ?? throw new InvalidOperationException("Wire source object is absent.");
                IGH_DocumentObject targetObject = document.FindObject(
                    expected.Target.ObjectGuid,
                    topLevelOnly: true)
                    ?? throw new InvalidOperationException("Wire target object is absent.");
                Require(
                    sourceObject.Attributes.Pivot.X < targetObject.Attributes.Pivot.X,
                    "Wire " + expected.SourceObjectGuid + " -> " + expected.Target.ObjectGuid
                        + " must flow from left to right on the example canvas.");
                Require(
                    source.Attributes.Pivot.X < target.Attributes.Pivot.X,
                    "Wire ports on " + expected.SourceObjectGuid + " -> " + expected.Target.ObjectGuid
                        + " must also flow from left to right without a reverse hook.");
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
            Require(
                graph.LinkedModel.BrepParameterGuids.Length > 0,
                graph.LinkedModel.FileName + " must bind at least one Brep parameter.");
            Require(
                graph.LinkedModel.CurveParameterGuids.Length > 0,
                graph.LinkedModel.FileName + " must bind at least one Curve parameter.");
            Brep[] breps = graph.LinkedModel.BrepParameterGuids
                .Select(guid => RequireObject<Param_Brep>(document, guid))
                .SelectMany(parameter => parameter.VolatileData.AllData(true))
                .OfType<GH_Brep>()
                .Select(item => item.Value)
                .Where(item => item is not null)
                .Cast<Brep>()
                .ToArray();
            Curve[] curves = graph.LinkedModel.CurveParameterGuids
                .Select(guid => RequireObject<Param_Curve>(document, guid))
                .SelectMany(parameter => parameter.VolatileData.AllData(true))
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

        if (exercise && inputs.CanRunEnergyPlusWorkflow)
        {
            string processTempRoot = Path.GetFullPath(Path.GetTempPath());
            Require(
                IsSameOrDescendant(processTempRoot, inputs.OutputDirectory),
                "The example host temp directory escaped repository temp evidence: " + processTempRoot);
        }

        return expectation switch
        {
            InvisibleRuntimeWorkflowExpectation invisible => ValidateInvisibleRuntimeWorkflow(
                document,
                invisible,
                inputs,
                exercise),
            SimpleRuntimeWorkflowExpectation simple => ValidateSimpleRuntimeWorkflow(
                document,
                simple,
                inputs,
                exercise),
            _ => throw new InvalidOperationException(
                "Unsupported runtime workflow expectation: " + expectation.GetType().FullName),
        };
    }

    private static RuntimeValidationFacts ValidateInvisibleRuntimeWorkflow(
        GH_Document document,
        InvisibleRuntimeWorkflowExpectation expectation,
        ExampleHostInputs inputs,
        bool exercise)
    {
        RequireEmptyFilePath(document, expectation.WeatherPathGuid);
        RequirePersistentBoolean(document, expectation.RunTriggerGuid, false);
        RequirePersistentBoolean(document, expectation.CancelTriggerGuid, false);
        RequirePersistentBoolean(document, expectation.ForceRerunGuid, false);

        if (!exercise)
        {
            return NotExecutedRuntime(
                "deferred",
                "The saved InvisibleDragon workflow, blank EPW input, and safe False triggers were validated; runtime exercise is deferred to the final reopened document.");
        }

        if (!inputs.CanRunEnergyPlusWorkflow)
        {
            return NotExecutedRuntime(
                inputs.EnergyPlusGateStatus,
                inputs.EnergyPlusGateReason);
        }

        string weatherPath = inputs.InvisibleEnergyPlusWeatherPath
            ?? throw new InvalidOperationException(
                "The ready EnergyPlus gate did not supply the EPW required for InvisibleDragon automation.");
        Require(File.Exists(weatherPath), "The InvisibleDragon automation EPW is absent: " + weatherPath);
        string workflowRoot = Path.Combine(inputs.OutputDirectory, "runtime-workflow", "invisibledragon");
        Require(!Directory.Exists(workflowRoot), "The runtime evidence directory already exists: " + workflowRoot);
        Directory.CreateDirectory(workflowRoot);

        try
        {
            SetFilePath(document, expectation.WeatherPathGuid, weatherPath);
            SetBoolean(document, expectation.RunTriggerGuid, false);
            SetBoolean(document, expectation.CancelTriggerGuid, false);
            SetBoolean(document, expectation.ForceRerunGuid, false);
            Solve(document);
            Require(
                ReadBoolean(document, expectation.WeatherComponentGuid, 1),
                "ID Weather did not verify the automation-only EPW. "
                    + RuntimeMessages(document, expectation.WeatherComponentGuid));
            RequireOutputType(
                document,
                expectation.WeatherComponentGuid,
                0,
                "GonieGonie.InvisibleDragon.Grasshopper.Types.PreparedWeatherFileGoo");
            RequireOutputType(
                document,
                expectation.CompileComponentGuid,
                0,
                "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonIdfGoo");
            Require(
                ReadBoolean(document, expectation.CompileComponentGuid, 2),
                "Compile InvisibleDragon did not report a valid managed IDF. "
                    + RuntimeMessages(document, expectation.CompileComponentGuid));

            SetBoolean(document, expectation.RunTriggerGuid, true);
            Solve(document);
            string firstRunState = WaitForTerminalState(
                document,
                expectation.RunComponentGuid,
                "_sync",
                "_activeTask",
                "_state",
                inputs.EnergyPlusWorkflowTimeout,
                "Succeeded",
                "Failed",
                "Cancelled",
                "TimedOut");
            SetBoolean(document, expectation.RunTriggerGuid, false);
            Solve(document);
            Require(
                string.Equals(firstRunState, "Succeeded", StringComparison.Ordinal),
                "The real InvisibleDragon example run ended in " + firstRunState + ". "
                    + RuntimeMessages(document, expectation.RunComponentGuid));
            Require(
                ReadBoolean(document, expectation.RunComponentGuid, 2),
                "The InvisibleDragon run did not report success.");
            RequireOutputType(
                document,
                expectation.RunComponentGuid,
                0,
                "GonieGonie.InvisibleDragon.Grasshopper.Types.EnergyPlusResultGoo");

            SetBoolean(document, expectation.RunTriggerGuid, true);
            Solve(document);
            string cachedRunState = WaitForTerminalState(
                document,
                expectation.RunComponentGuid,
                "_sync",
                "_activeTask",
                "_state",
                inputs.EnergyPlusWorkflowTimeout,
                "Cached",
                "Succeeded",
                "Failed",
                "Cancelled",
                "TimedOut");
            SetBoolean(document, expectation.RunTriggerGuid, false);
            Solve(document);
            Require(
                string.Equals(cachedRunState, "Cached", StringComparison.Ordinal),
                "An identical InvisibleDragon rerun did not use the component cache; state was "
                    + cachedRunState + ".");
            Require(
                ReadBoolean(document, expectation.RunComponentGuid, 2),
                "The cached InvisibleDragon result lost its success state.");

            SetBoolean(document, expectation.ForceRerunGuid, true);
            Solve(document);
            SetBoolean(document, expectation.RunTriggerGuid, true);
            Solve(document);
            SetBoolean(document, expectation.CancelTriggerGuid, true);
            Solve(document);
            string cancellationState = WaitForTerminalState(
                document,
                expectation.RunComponentGuid,
                "_sync",
                "_activeTask",
                "_state",
                inputs.EnergyPlusWorkflowTimeout,
                "Cancelled",
                "Succeeded",
                "Failed",
                "TimedOut");
            Require(
                string.Equals(cancellationState, "Cancelled", StringComparison.Ordinal),
                "The explicit InvisibleDragon cancellation exercise ended in " + cancellationState + ".");
            Solve(document);
            Require(
                !ReadBoolean(document, expectation.RunComponentGuid, 2),
                "A cancelled InvisibleDragon run incorrectly reported success.");

            return new RuntimeValidationFacts(
                "ready",
                inputs.EnergyPlusGateReason,
                true,
                cancellationState,
                firstRunState,
                cachedRunState,
                cancellationState,
                "Not Run",
                "Not Run",
                "Not Run",
                true,
                false,
                true,
                true,
                false,
                false,
                Path.GetFullPath(inputs.OutputDirectory),
                null,
                Array.Empty<string>(),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }
        finally
        {
            BestEffortCancelAndDrain(document, expectation, inputs.EnergyPlusWorkflowTimeout);
        }
    }

    private static RuntimeValidationFacts ValidateSimpleRuntimeWorkflow(
        GH_Document document,
        SimpleRuntimeWorkflowExpectation expectation,
        ExampleHostInputs inputs,
        bool exercise)
    {
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

        string workflowRoot = Path.Combine(inputs.OutputDirectory, "runtime-workflow", "simpledragon");
        string csvRoot = Path.Combine(workflowRoot, "csv-package");
        Require(!Directory.Exists(workflowRoot), "The runtime evidence directory already exists: " + workflowRoot);
        Directory.CreateDirectory(workflowRoot);
        DateTime evidenceNotBeforeUtc = DateTime.UtcNow.AddSeconds(-2);

        try
        {
            SetPanel(document, expectation.ExportDirectoryGuid, csvRoot);
            SetBoolean(document, expectation.RunTriggerGuid, false);
            SetBoolean(document, expectation.CancelTriggerGuid, false);
            SetBoolean(document, expectation.ForceRerunGuid, false);
            SetBoolean(document, expectation.ExportTriggerGuid, false);
            SetBoolean(document, expectation.OverwriteGuid, false);
            Solve(document);

            SetBoolean(document, expectation.RunTriggerGuid, true);
            Solve(document);
            string firstRunState = WaitForTerminalState(
                document,
                expectation.RunComponentGuid,
                "_sync",
                "_activeTask",
                "_state",
                inputs.EnergyPlusWorkflowTimeout,
                "Succeeded",
                "Failed",
                "Cancelled");
            SetBoolean(document, expectation.RunTriggerGuid, false);
            Solve(document);
            Require(
                string.Equals(firstRunState, "Succeeded", StringComparison.Ordinal),
                "The real SimpleDragon example run ended in " + firstRunState + ". "
                    + RuntimeMessages(document, expectation.RunComponentGuid));
            Require(ReadBoolean(document, expectation.RunComponentGuid, 2), "The SimpleDragon run did not report success.");
            RequireOutputType(
                document,
                expectation.RunComponentGuid,
                0,
                "GonieGonie.SimpleDragon.Grasshopper.Types.GreenRetrofitResultGoo");
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
            RequireValidCurveOutput(
                document,
                expectation.MonthlyLinePlotGuid,
                0,
                5,
                "default monthly fuel lines");
            RequireValidCurveOutput(
                document,
                expectation.MonthlyLinePlotGuid,
                1,
                1,
                "default monthly plot frame");
            RequireValidCurveOutput(
                document,
                expectation.MonthlyLinePlotGuid,
                2,
                1,
                "default monthly plot zero axis");
            Require(
                string.Equals(ReadString(document, expectation.MonthlyLinePlotGuid, 7), "kWh/m2", StringComparison.Ordinal),
                "The zero-configuration monthly plot did not use the SiteUses per-area default.");
            string[] plotErrors = RequireObject<GH_ActiveObject>(document, expectation.MonthlyLinePlotGuid)
                .RuntimeMessages(GH_RuntimeMessageLevel.Error)
                .ToArray();
            Require(
                plotErrors.Length == 0,
                "The zero-configuration monthly plot reported errors: " + string.Join(" | ", plotErrors));

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
                csvContents.Any(content => content.Contains("goniegonie-simpledragon-csv-export.v2")
                    && content.Contains("\"csv_schema\": \"2\"")),
                "The CSV package manifest does not identify its schema.");
            int summaryCsvIndex = Array.FindIndex(
                csvNames,
                name => string.Equals(name, "summary.csv", StringComparison.Ordinal));
            Require(summaryCsvIndex >= 0, "The CSV package contains no summary.csv file.");
            string csvCaseId = RequireSummaryCsvMatchesResult(
                csvContents[summaryCsvIndex],
                totalArea,
                annualResult);
            Require(
                csvContents.Any(content => content.Contains("goniegonie-simpledragon-csv-export.v2")
                    && content.Contains(csvCaseId)),
                "The CSV package manifest does not preserve its model-derived case ID.");
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
                "_sync",
                "_activeTask",
                "_state",
                inputs.EnergyPlusWorkflowTimeout,
                "Cached",
                "Succeeded",
                "Failed",
                "Cancelled");
            SetBoolean(document, expectation.RunTriggerGuid, false);
            Solve(document);
            Require(
                string.Equals(cachedRunState, "Cached", StringComparison.Ordinal),
                "An identical SimpleDragon rerun did not use the component cache; state was " + cachedRunState + ".");
            Require(ReadBoolean(document, expectation.RunComponentGuid, 2), "The cached SimpleDragon result lost its success state.");
            RequireOutputType(
                document,
                expectation.RunComponentGuid,
                0,
                "GonieGonie.SimpleDragon.Grasshopper.Types.GreenRetrofitResultGoo");

            SetBoolean(document, expectation.ForceRerunGuid, true);
            Solve(document);
            SetBoolean(document, expectation.RunTriggerGuid, true);
            Solve(document);
            SetBoolean(document, expectation.CancelTriggerGuid, true);
            Solve(document);
            string cancellationState = WaitForTerminalState(
                document,
                expectation.RunComponentGuid,
                "_sync",
                "_activeTask",
                "_state",
                inputs.EnergyPlusWorkflowTimeout,
                "Cancelled",
                "Succeeded",
                "Failed");
            Require(
                string.Equals(cancellationState, "Cancelled", StringComparison.Ordinal),
                "The explicit SimpleDragon cancellation exercise ended in " + cancellationState + ".");
            Solve(document);
            Require(
                !ReadBoolean(document, expectation.RunComponentGuid, 2),
                "A cancelled SimpleDragon run incorrectly reported success.");
            SetBoolean(document, expectation.RunTriggerGuid, false);
            SetBoolean(document, expectation.CancelTriggerGuid, false);
            SetBoolean(document, expectation.ForceRerunGuid, false);
            Solve(document);

            string managedBatchRoot = Path.Combine(inputs.OutputDirectory, "b");
            SetPanel(
                document,
                expectation.BatchModelNameGuid,
                "Two-Zone Office Runtime " + Guid.NewGuid().ToString("N"));
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
            string batchCaseId = RequireSingleBatchCase(
                document,
                expectation.BatchComponentGuid,
                "Succeeded");
            string combinedCsv = RequireFreshContainedFile(
                ReadString(document, expectation.BatchComponentGuid, 3),
                managedBatchRoot,
                evidenceNotBeforeUtc,
                "SimpleDragon batch combined CSV");
            string manifest = RequireFreshContainedFile(
                ReadString(document, expectation.BatchComponentGuid, 4),
                managedBatchRoot,
                evidenceNotBeforeUtc,
                "SimpleDragon batch manifest");
            string combinedCsvContent = File.ReadAllText(combinedCsv);
            string manifestContent = File.ReadAllText(manifest);
            Require(
                combinedCsvContent.StartsWith("index,case_id,status", StringComparison.Ordinal)
                    && combinedCsvContent.Contains(batchCaseId + ",Succeeded"),
                "The SimpleDragon batch combined CSV does not contain the successful ordered case.");
            RequireBatchCsvMatchesResult(combinedCsvContent, batchCaseId, totalArea, annualResult);
            Require(
                manifestContent.Contains("goniegonie.simple-dragon.batch-manifest.v1")
                    && manifestContent.Contains(batchCaseId)
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
            Require(
                string.Equals(
                    RequireSingleBatchCase(document, expectation.BatchComponentGuid, "Succeeded"),
                    batchCaseId,
                    StringComparison.Ordinal),
                "The identical SimpleDragon batch rerun changed its model-derived case ID.");

            SetPanel(
                document,
                expectation.BatchModelNameGuid,
                "Two-Zone Office Cancellation " + Guid.NewGuid().ToString("N"));
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
            string cancelledBatchCaseId = RequireSingleBatchCase(
                document,
                expectation.BatchComponentGuid,
                "Cancelled");
            string cancelledBatchCsv = RequireFreshContainedFile(
                ReadString(document, expectation.BatchComponentGuid, 3),
                managedBatchRoot,
                evidenceNotBeforeUtc,
                "Cancelled SimpleDragon batch combined CSV");
            string cancelledBatchManifest = RequireFreshContainedFile(
                ReadString(document, expectation.BatchComponentGuid, 4),
                managedBatchRoot,
                evidenceNotBeforeUtc,
                "Cancelled SimpleDragon batch manifest");
            Require(
                File.ReadAllText(cancelledBatchCsv).Contains(cancelledBatchCaseId + ",Cancelled"),
                "The cancelled batch CSV does not preserve the cancelled case status.");
            string cancelledManifestContent = File.ReadAllText(cancelledBatchManifest);
            Require(
                cancelledManifestContent.Contains(cancelledBatchCaseId)
                    && cancelledManifestContent.Contains("\"status\": \"Cancelled\""),
                "The cancelled batch manifest does not preserve the model-derived case ID and cancelled status.");

            string finalRunState = ReadRuntimeSnapshot(
                document,
                expectation.RunComponentGuid,
                "_sync",
                "_activeTask",
                "_state").State;
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
                combinedCsv.Length == 0 ? string.Empty : ComputeSha256(combinedCsv),
                manifest.Length == 0 ? string.Empty : ComputeSha256(manifest),
                cancelledBatchCsv.Length == 0 ? string.Empty : ComputeSha256(cancelledBatchCsv),
                cancelledBatchManifest.Length == 0 ? string.Empty : ComputeSha256(cancelledBatchManifest));
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
        SimpleRuntimeWorkflowExpectation expectation,
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
                "_sync",
                "_activeTask",
                "_state");
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
                        "_sync",
                        "_activeTask",
                        "_state");
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

    private static void BestEffortCancelAndDrain(
        GH_Document document,
        InvisibleRuntimeWorkflowExpectation expectation,
        TimeSpan workflowTimeout)
    {
        TimeSpan drainTimeout = TimeSpan.FromSeconds(Math.Max(5, Math.Min(30, workflowTimeout.TotalSeconds)));
        try
        {
            SetBoolean(document, expectation.RunTriggerGuid, false);
            SetBoolean(document, expectation.CancelTriggerGuid, false);
            SetBoolean(document, expectation.ForceRerunGuid, false);
            Solve(document);

            RuntimeComponentSnapshot run = ReadRuntimeSnapshot(
                document,
                expectation.RunComponentGuid,
                "_sync",
                "_activeTask",
                "_state");
            if (run.HasActiveTask)
            {
                SetBoolean(document, expectation.CancelTriggerGuid, true);
                Solve(document);
                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed < drainTimeout)
                {
                    run = ReadRuntimeSnapshot(
                        document,
                        expectation.RunComponentGuid,
                        "_sync",
                        "_activeTask",
                        "_state");
                    if (!run.HasActiveTask)
                    {
                        break;
                    }

                    System.Threading.Thread.Sleep(25);
                }

                if (run.HasActiveTask)
                {
                    Console.Error.WriteLine(
                        "InvisibleDragon runtime cleanup could not drain the active task within "
                            + drainTimeout.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " seconds.");
                }
            }

            SetBoolean(document, expectation.RunTriggerGuid, false);
            SetBoolean(document, expectation.CancelTriggerGuid, false);
            SetBoolean(document, expectation.ForceRerunGuid, false);
            SetFilePath(document, expectation.WeatherPathGuid, null);
            Solve(document);
            RequireEmptyFilePath(document, expectation.WeatherPathGuid);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "InvisibleDragon runtime workflow cleanup failed: "
                    + exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static string RequireSingleBatchCase(
        GH_Document document,
        Guid componentGuid,
        string expectedStatus)
    {
        string[] caseIds = ReadStrings(document, componentGuid, 1);
        string[] caseStatuses = ReadStrings(document, componentGuid, 2);
        Require(caseIds.Length == 1, "The single-case example batch did not emit exactly one case ID.");
        Require(
            !string.IsNullOrWhiteSpace(caseIds[0]),
            "The single-case example batch emitted an empty model-derived case ID.");
        Require(
            caseStatuses.Length == 1 && string.Equals(caseStatuses[0], expectedStatus, StringComparison.Ordinal),
            "The single-case example batch status was not " + expectedStatus + ".");
        return caseIds[0];
    }

    private static string RequireSummaryCsvMatchesResult(
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
        string? derivedCaseId = null;
        foreach (string[] row in rows.Skip(1))
        {
            Require(row.Length == expectedHeader.Length, "The SimpleDragon summary CSV contains a malformed row.");
            Require(
                !string.IsNullOrWhiteSpace(row[0]),
                "The SimpleDragon summary CSV contains an empty model-derived case ID.");
            derivedCaseId ??= row[0];
            Require(
                string.Equals(row[0], derivedCaseId, StringComparison.Ordinal),
                "The SimpleDragon summary CSV changed its model-derived case ID between rows.");
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
        return derivedCaseId!;
    }

    private static void RequireBatchCsvMatchesResult(
        string content,
        string expectedCaseId,
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
            string.Equals(row[caseIndex], expectedCaseId, StringComparison.Ordinal)
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

    private static void SetFilePath(GH_Document document, Guid instanceGuid, string? value)
    {
        Param_FilePath parameter = RequireObject<Param_FilePath>(document, instanceGuid);
        parameter.PersistentData.Clear();
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameter.PersistentData.Append(new GH_String(Path.GetFullPath(value)));
        }

        parameter.ExpireSolution(false);
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

    private static void RequireEmptyFilePath(GH_Document document, Guid instanceGuid)
    {
        Param_FilePath parameter = RequireObject<Param_FilePath>(document, instanceGuid);
        Require(
            !parameter.PersistentData.AllData(true).Any(),
            instanceGuid + " must remain data-empty in the saved Grasshopper definition.");
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

    private static void RequireValidCurveOutput(
        GH_Document document,
        Guid componentGuid,
        int outputIndex,
        int expectedCount,
        string label)
    {
        object[] values = ResolveParam(document, componentGuid, outputIndex, output: true)
            .VolatileData
            .AllData(true)
            .ToArray();
        GH_Curve[] curves = values.OfType<GH_Curve>().ToArray();
        Require(
            values.Length == expectedCount
                && curves.Length == expectedCount
                && curves.All(curve => curve.Value is not null && curve.Value.IsValid),
            componentGuid + " produced invalid " + label + "; expected " + expectedCount
                + " valid curves but received " + values.Length + ".");
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
            GH_Curve[] values = parameter.VolatileData.AllData(true).OfType<GH_Curve>().ToArray();
            Require(values.Length > 0, curveParameterGuid + " contains no curve.");
            foreach (GH_Curve goo in values)
            {
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
        internal static readonly ComponentIdentity InvisibleLayer = I("d15984d5-cd3f-4798-a67c-73138b54859e", "ConstructionLayerComponent");
        internal static readonly ComponentIdentity InvisibleConstruction = I("6d5a9b54-8a9e-4c95-91df-469e21a783c9", "LayeredConstructionComponent");
        internal static readonly ComponentIdentity InvisibleNoMass = I("e292a44e-9d8d-4796-95fb-126f77e83796", "NoMassConstructionComponent");
        internal static readonly ComponentIdentity InvisibleProfile = I("3d5717de-1b16-406a-91e0-7a392c08aa51", "ConstantProfileComponent");
        internal static readonly ComponentIdentity InvisibleGlazing = I("ecfd5cdd-3e4c-4261-8ddd-ecea8eaf5599", "GlazingComponent");
        internal static readonly ComponentIdentity InvisibleWindow = I("54bb0065-1b10-420c-a90e-0ce75e746781", "WindowFromPolylineComponent");
        internal static readonly ComponentIdentity InvisibleDoor = I("b2e1e805-a126-44fe-bf6c-4dbf16a76aae", "DoorFromPolylineComponent");
        internal static readonly ComponentIdentity InvisibleFloor = I("1938b273-3a60-459b-beb2-92e7c4905053", "FloorComponent");
        internal static readonly ComponentIdentity InvisibleCeiling = I("d1930bb6-4398-46b9-a661-451370f09103", "CeilingComponent");
        internal static readonly ComponentIdentity InvisibleWall = I("20a8a2f5-845e-4a46-aa03-fb8849f592e2", "WallComponent");
        internal static readonly ComponentIdentity InvisibleZone = I("21ece4e9-87dd-4f34-9b95-8bc87fb0bfd2", "ZoneComponent");
        internal static readonly ComponentIdentity InvisibleHeatPump = I("e8751fda-24b9-4727-ad66-f81de722f64f", "HeatPumpComponent");
        internal static readonly ComponentIdentity InvisibleAirHandler = I("a3a4afd8-17e1-4d9f-8da5-5883331c360f", "AirHandlingUnitComponent");
        internal static readonly ComponentIdentity InvisibleBoiler = I("e732f5f9-db94-405b-9221-f4449b4baad7", "BoilerComponent");
        internal static readonly ComponentIdentity InvisibleRadiantFloor = I("e3bd88b6-54b6-43ec-9c94-ee0e36218618", "RadiantFloorComponent");
        internal static readonly ComponentIdentity InvisibleErv = I("3d5f630e-66c3-43da-b73c-50d5be1792c3", "EnergyRecoveryVentilatorComponent");
        internal static readonly ComponentIdentity InvisiblePv = I("237bc85d-769a-468b-a048-70e3b5c382ee", "PhotovoltaicPanelComponent");
        internal static readonly ComponentIdentity InvisibleModel = I("057ee08b-759f-43e0-8ab8-625747d951ef", "EnergyModelComponent");
        internal static readonly ComponentIdentity InvisibleCompile = I("e3e4d8f9-4fd8-4b17-9ec7-a27cb5627802", "CompileInvisibleDragonComponent");
        internal static readonly ComponentIdentity InvisibleWeather = I("4f443564-2e13-4a79-8845-27d1e6eb285d", "VerifyInvisibleDragonWeatherComponent");
        internal static readonly ComponentIdentity InvisibleManagedRun = I("50e4f5bf-f174-458f-bfaa-aaf4e25ce5b5", "ManagedRunEnergyPlusComponent");
        internal static readonly ComponentIdentity InvisibleResultSummary = I("31967aee-84ae-4536-b091-b301d1ab2c3d", "EnergyPlusResultSummaryComponent");
        internal static readonly ComponentIdentity SimpleMaterial = S("fee586e8-692c-407e-a803-d5c43f3c7222", "SimpleDragonMaterialComponent");
        internal static readonly ComponentIdentity SimpleLayer = S("b97da4a1-7b1c-472a-a4b0-83603e202c2b", "SimpleDragonSurfaceConstructionLayerComponent");
        internal static readonly ComponentIdentity SimpleConstruction = S("3e1fa67f-dbb2-4c19-b54b-226c295f5751", "SimpleDragonSurfaceConstructionComponent");
        internal static readonly ComponentIdentity SimpleFenestration = S("b9af07b4-d08e-4335-ab55-a6fd33cb1a93", "SimpleDragonFenestrationConstructionComponent");
        internal static readonly ComponentIdentity SimpleProfile = S("fb92c938-41e1-475f-ad03-ca6a1a8e42e1", "LookupUsageProfileComponent");
        internal static readonly ComponentIdentity SimpleHeatPump = S("e6e14d7b-55b4-45a9-97f9-9b99715f5ebc", "SimpleDragonHeatPumpComponent");
        internal static readonly ComponentIdentity SimpleAirHandler = S("8b0839fc-d03d-46af-8897-1ba4a41eab46", "SimpleDragonAirHandlingUnitComponent");
        internal static readonly ComponentIdentity SimpleBoiler = S("7b973e2c-7254-4730-9326-c320abedde5a", "SimpleDragonBoilerComponent");
        internal static readonly ComponentIdentity SimpleRadiator = S("2e77eee2-c354-40ba-abae-b501373046bc", "SimpleDragonRadiatorComponent");
        internal static readonly ComponentIdentity SimpleElectricRadiator = S("3a3f5157-23bb-4094-83fd-e5cf4dc4d891", "SimpleDragonElectricRadiatorComponent");
        internal static readonly ComponentIdentity SimpleChiller = S("d5cedc15-8b76-49e3-842b-5b0c498556fd", "SimpleDragonChillerComponent");
        internal static readonly ComponentIdentity SimpleFanCoil = S("dd41df8f-9e3e-4663-8ce7-89025cfde30c", "SimpleDragonFanCoilUnitComponent");
        internal static readonly ComponentIdentity SimpleErv = S("15afd6e6-1c05-4715-909b-b6e98ef91375", "SimpleDragonEnergyRecoveryVentilatorComponent");
        internal static readonly ComponentIdentity SimplePv = S("7fcb5c47-3d49-4aa0-8fbc-bd765711401f", "SimpleDragonPhotovoltaicPanelComponent");
        internal static readonly ComponentIdentity SimpleOpening = S("7d41fd2c-b93f-4fc8-88ea-db1f3abeb2f1", "CreateSimpleDragonOpeningComponent");
        internal static readonly ComponentIdentity SimpleFloor = S("e15d7475-e5cf-4e37-81a4-e656c69ee250", "CreateSimpleDragonFloorComponent");
        internal static readonly ComponentIdentity SimpleCeiling = S("39e2ad8c-8fbb-40bd-84cc-218de37bb720", "CreateSimpleDragonCeilingComponent");
        internal static readonly ComponentIdentity SimpleWall = S("2c0bc0e2-df1d-4e42-9b97-d841e8c83214", "CreateSimpleDragonWallComponent");
        internal static readonly ComponentIdentity SimpleZone = S("30b8e2c4-207a-4cf5-9801-ac4ae16d33e2", "CreateSimpleDragonZoneComponent");
        internal static readonly ComponentIdentity SimpleModel = S("ce38124b-f99b-4d09-be3b-e5e5717db707", "CreateSimpleDragonModelComponent");
        internal static readonly ComponentIdentity SimpleRun = S("6e242e51-77ce-4f77-8445-a17d636c7310", "RunSimpleDragonComponent");
        internal static readonly ComponentIdentity SimpleReadResult = S("a03fb1d7-7ae2-4e2c-ab31-0e626af50163", "ReadGreenRetrofitResultComponent");
        internal static readonly ComponentIdentity SimpleResultSummary = S("577809aa-2d1c-40ea-aa50-f71d73f19f83", "GreenRetrofitResultSummaryComponent");
        internal static readonly ComponentIdentity SimpleDataTree = S("cb5a98f8-4188-4323-b55d-795b4a7ba20e", "GreenRetrofitDataTreeComponent");
        internal static readonly ComponentIdentity SimpleLinePlot = S("76e0c1b6-68d6-4cdc-a418-eea18aa131c1", "GreenRetrofitMonthlyLinePlotComponent");
        internal static readonly ComponentIdentity SimpleBarPlot = S("a73acba4-d98d-4fec-a846-dc982256d6b1", "GreenRetrofitMonthlyBarPlotComponent");
        internal static readonly ComponentIdentity SimpleExportCsv = S("9fe8a410-ea95-4eb8-81ec-56c45cdd029c", "ExportGreenRetrofitCsvComponent");
        internal static readonly ComponentIdentity SimpleBatchCase = S("11336c6a-5bd4-4d6b-80a1-89bd168f8d54", "SimpleDragonBatchCaseComponent");
        internal static readonly ComponentIdentity SimpleManagedBatch = S("e0a54494-3d69-4681-8756-cc3cd86df4e1", "ManagedRunSimpleDragonBatchComponent");
        internal static IReadOnlyList<ComponentIdentity> All { get; } = new[]
        {
            InvisibleMaterial, InvisibleLayer, InvisibleConstruction, InvisibleNoMass, InvisibleProfile,
            InvisibleGlazing, InvisibleWindow, InvisibleDoor, InvisibleFloor, InvisibleCeiling, InvisibleWall,
            InvisibleZone, InvisibleHeatPump, InvisibleAirHandler, InvisibleBoiler, InvisibleRadiantFloor,
            InvisibleErv, InvisiblePv, InvisibleModel, InvisibleCompile,
            InvisibleWeather, InvisibleManagedRun, InvisibleResultSummary,
            SimpleMaterial, SimpleLayer, SimpleConstruction, SimpleFenestration, SimpleProfile, SimpleHeatPump,
            SimpleAirHandler, SimpleBoiler, SimpleRadiator, SimpleElectricRadiator, SimpleChiller, SimpleFanCoil, SimpleErv,
            SimplePv, SimpleOpening, SimpleFloor, SimpleCeiling, SimpleWall, SimpleZone, SimpleModel, SimpleRun,
            SimpleReadResult, SimpleResultSummary, SimpleDataTree,
            SimpleLinePlot, SimpleBarPlot, SimpleExportCsv, SimpleBatchCase, SimpleManagedBatch,
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
    private static readonly char[] NoteWordSeparators = { ' ' };
    private readonly GH_ComponentServer _server;
    private readonly string _instancePrefix;
    private readonly GH_Document _document = new();
    private readonly List<ObjectExpectation> _objects = new();
    private readonly List<WireExpectation> _wires = new();
    private readonly List<OutputExpectation> _outputs = new();
    private readonly List<BooleanExpectation> _booleans = new();
    private readonly List<NumberExpectation> _numbers = new();
    private readonly List<NoteExpectation> _notes = new();
    private readonly List<GroupExpectation> _groups = new();

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
        _objects.Add(new ObjectExpectation(
            node.InstanceGuid,
            identity.TypeName,
            identity.Id,
            new System.Drawing.PointF(x, y)));
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

    internal GraphNode Note(int key, string text, float x, float y)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Canvas notes must contain explanatory text.", nameof(text));
        }

        string formattedText = WrapNote(text, 62);
        var scribble = new GH_Scribble
        {
            Text = formattedText,
            Font = GH_FontServer.Large,
        };
        GraphNode node = AddSpecial(key, scribble, x, y);
        _notes.Add(new NoteExpectation(node.InstanceGuid, formattedText));
        return node;
    }

    internal GraphNode Group(
        int key,
        string name,
        ExampleGroupTheme theme,
        params GraphNode[] members)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Canvas groups must have a visible name.", nameof(name));
        }

        GraphNode[] distinctMembers = members
            .GroupBy(member => member.InstanceGuid)
            .Select(group => group.First())
            .ToArray();
        if (distinctMembers.Length < 2)
        {
            throw new ArgumentException("Canvas groups must contain at least two objects.", nameof(members));
        }

        if (distinctMembers.Length != members.Length)
        {
            throw new ArgumentException("Canvas groups cannot repeat a member.", nameof(members));
        }

        if (distinctMembers.Any(member => member.Object is GH_Group
            || !ReferenceEquals(_document.FindObject(member.InstanceGuid, topLevelOnly: true), member.Object)))
        {
            throw new ArgumentException(
                "Canvas groups can contain only top-level objects from the current example document.",
                nameof(members));
        }

        var group = new GH_Group
        {
            NickName = name,
            Border = GH_GroupBorder.Box,
            Colour = ExamplePresentation.GroupColour(theme),
        };
        GraphNode node = AddSpecial(key, group, 0, 0);
        foreach (GraphNode member in distinctMembers)
        {
            group.AddObject(member.InstanceGuid);
        }

        group.ExpireCaches();
        group.Attributes.ExpireLayout();
        group.Attributes.PerformLayout();
        _groups.Add(new GroupExpectation(
            node.InstanceGuid,
            name,
            group.Border,
            group.Colour.ToArgb(),
            distinctMembers.Select(member => member.InstanceGuid).ToArray()));
        return node;
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

    internal GraphNode Strings(int key, string nickName, IEnumerable<string> values, float x, float y)
    {
        var parameter = new Param_String { NickName = nickName };
        foreach (string value in values)
        {
            parameter.PersistentData.Append(new GH_String(value));
        }

        return AddSpecial(key, parameter, x, y);
    }

    internal GraphNode ValueList(
        int key,
        string nickName,
        IEnumerable<string> values,
        string selectedValue,
        float x,
        float y)
    {
        var valueList = new GH_ValueList
        {
            NickName = nickName,
            ListMode = GH_ValueListMode.DropDown,
        };
        valueList.ListItems.Clear();
        bool selected = false;
        foreach (string value in values)
        {
            var item = new GH_ValueListItem(value, "\"" + value.Replace("\"", "\\\"") + "\"")
            {
                Selected = string.Equals(value, selectedValue, StringComparison.Ordinal),
            };
            selected |= item.Selected;
            valueList.ListItems.Add(item);
        }

        if (!selected)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectedValue),
                selectedValue,
                "The selected Value List item must be one of the supplied choices.");
        }

        return AddSpecial(key, valueList, x, y);
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

    internal GraphNode EmptyFilePath(
        int key,
        string nickName,
        string fileFilter,
        float x,
        float y)
    {
        var parameter = new Param_FilePath
        {
            NickName = nickName,
            ExpireOnFileEvent = false,
            FileFilter = fileFilter,
        };
        return AddSpecial(key, parameter, x, y);
    }

    internal void Connect(GraphNode source, int? sourceOutput, GraphNode target, int? targetInput)
    {
        IGH_Param sourceParam = Parameter(source.Object, sourceOutput, output: true);
        IGH_Param targetParam = Parameter(target.Object, targetInput, output: false);
        string parameterType = sourceParam.GetType().Name;
        bool exclusiveOwnershipWire = sourceParam.GetType() == targetParam.GetType()
            && (parameterType == "SimpleDragonOpeningDefinitionParam"
                || parameterType == "SimpleDragonSurfaceDefinitionParam"
                || parameterType == "SimpleDragonSupplySystemParam"
                || parameterType == "SimpleDragonZoneErvParam");
        if (exclusiveOwnershipWire
            && _wires.Any(item => item.SourceObjectGuid == source.InstanceGuid
                && item.SourceOutputIndex == sourceOutput
                && string.Equals(
                    item.TargetParameterType,
                    targetParam.GetType().FullName,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                sourceParam.Name + " is child-owned and cannot be wired to more than one owning input.");
        }

        targetParam.AddSource(sourceParam);
        _wires.Add(new WireExpectation(
            source.InstanceGuid,
            sourceOutput,
            new TargetKey(target.InstanceGuid, targetInput),
            sourceParam.Name,
            sourceParam.GetType().FullName!,
            sourceParam.Access,
            targetParam.Name,
            targetParam.GetType().FullName!,
            targetParam.Access));
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
        if (_notes.Count == 0 || _groups.Count == 0)
        {
            throw new InvalidOperationException("Every advanced example must contain canvas notes and native groups.");
        }

        Guid[] groupableObjectGuids = _objects
            .Where(item => !string.Equals(item.TypeName, typeof(GH_Group).FullName, StringComparison.Ordinal))
            .Select(item => item.InstanceGuid)
            .ToArray();
        Guid[] groupedObjectGuids = _groups.SelectMany(group => group.MemberGuids).ToArray();
        if (groupedObjectGuids.Length != groupableObjectGuids.Length
            || !groupedObjectGuids.ToHashSet().SetEquals(groupableObjectGuids))
        {
            throw new InvalidOperationException(
                "Every example object must belong to exactly one meaningful native group.");
        }

        return new ScenarioGraph(
            product,
            _document,
            _objects.ToArray(),
            _wires.ToArray(),
            _outputs.ToArray(),
            _booleans.ToArray(),
            _numbers.ToArray(),
            _notes.ToArray(),
            _groups.ToArray(),
            primaryOutputGooType,
            linkedModel,
            envelope,
            runtimeWorkflow);
    }

    private GraphNode AddSpecial<T>(int key, T value, float x, float y)
        where T : class, IGH_DocumentObject
    {
        GraphNode node = Add(key, value, x, y);
        _objects.Add(new ObjectExpectation(
            node.InstanceGuid,
            value.GetType().FullName!,
            null,
            new System.Drawing.PointF(x, y)));
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

        value.Attributes.ExpireLayout();
        value.Attributes.PerformLayout();

        return new GraphNode(id, value);
    }

    private Guid InstanceGuid(int key)
    {
        return new Guid(_instancePrefix + "-0000-4000-8000-" + key.ToString("D12", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string WrapNote(string text, int maximumLineLength)
    {
        var lines = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (string word in text.Split(NoteWordSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > maximumLineLength)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(word);
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return string.Join(Environment.NewLine, lines);
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

internal sealed record ObjectExpectation(
    Guid InstanceGuid,
    string TypeName,
    Guid? ComponentGuid,
    System.Drawing.PointF Pivot);

internal sealed record TargetKey(Guid ObjectGuid, int? Index);

internal sealed record WireExpectation(
    Guid SourceObjectGuid,
    int? SourceOutputIndex,
    TargetKey Target,
    string SourceParameterName,
    string SourceParameterType,
    GH_ParamAccess SourceAccess,
    string TargetParameterName,
    string TargetParameterType,
    GH_ParamAccess TargetAccess);

internal sealed record OutputExpectation(Guid ObjectGuid, int? OutputIndex, int MinimumCount, string? GooType);

internal sealed record BooleanExpectation(Guid ObjectGuid, int OutputIndex, bool Expected);

internal sealed record NumberExpectation(Guid ObjectGuid, int OutputIndex, double Expected, double Tolerance);

internal sealed record NoteExpectation(Guid InstanceGuid, string Text);

internal sealed record GroupExpectation(
    Guid InstanceGuid,
    string Name,
    GH_GroupBorder Border,
    int ColourArgb,
    Guid[] MemberGuids);

internal sealed record LinkedModelExpectation(
    string FileName,
    Guid[] BrepParameterGuids,
    Guid[] CurveParameterGuids);

internal sealed record OutwardEnvelopeExpectation(Guid[] CurveParameterGuids, Point3d ZoneCentroid);

internal abstract record RuntimeWorkflowExpectation;

internal sealed record InvisibleRuntimeWorkflowExpectation(
    Guid CompileComponentGuid,
    Guid WeatherPathGuid,
    Guid WeatherComponentGuid,
    Guid RunComponentGuid,
    Guid RunTriggerGuid,
    Guid CancelTriggerGuid,
    Guid ForceRerunGuid) : RuntimeWorkflowExpectation;

internal sealed record SimpleRuntimeWorkflowExpectation(
    Guid RunComponentGuid,
    Guid RunTriggerGuid,
    Guid CancelTriggerGuid,
    Guid ForceRerunGuid,
    Guid ResultSummaryGuid,
    Guid MonthlyLinePlotGuid,
    Guid ExportCsvGuid,
    Guid ExportDirectoryGuid,
    Guid ExportTriggerGuid,
    Guid OverwriteGuid,
    Guid BatchComponentGuid,
    Guid BatchModelNameGuid,
    Guid BatchRunTriggerGuid,
    Guid BatchCancelTriggerGuid) : RuntimeWorkflowExpectation;

internal sealed record ScenarioGraph(
    string Product,
    GH_Document Document,
    IReadOnlyList<ObjectExpectation> Objects,
    IReadOnlyList<WireExpectation> Wires,
    IReadOnlyList<OutputExpectation> Outputs,
    IReadOnlyList<BooleanExpectation> Booleans,
    IReadOnlyList<NumberExpectation> Numbers,
    IReadOnlyList<NoteExpectation> Notes,
    IReadOnlyList<GroupExpectation> Groups,
    string PrimaryOutputGooType,
    LinkedModelExpectation? LinkedModel,
    OutwardEnvelopeExpectation? Envelope,
    RuntimeWorkflowExpectation? RuntimeWorkflow);

internal enum ExampleGroupTheme
{
    Inputs,
    Envelope,
    Systems,
    Model,
    Runtime,
    Results,
}

internal static class ExamplePresentation
{
    internal static System.Drawing.Color GroupColour(ExampleGroupTheme theme)
    {
        return theme switch
        {
            ExampleGroupTheme.Inputs => System.Drawing.Color.FromArgb(255, 221, 235, 250),
            ExampleGroupTheme.Envelope => System.Drawing.Color.FromArgb(255, 225, 243, 226),
            ExampleGroupTheme.Systems => System.Drawing.Color.FromArgb(255, 255, 238, 203),
            ExampleGroupTheme.Model => System.Drawing.Color.FromArgb(255, 232, 224, 246),
            ExampleGroupTheme.Runtime => System.Drawing.Color.FromArgb(255, 255, 224, 224),
            ExampleGroupTheme.Results => System.Drawing.Color.FromArgb(255, 221, 242, 241),
            _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
        };
    }
}
