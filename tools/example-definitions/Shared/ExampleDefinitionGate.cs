using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace Dragons.ExampleDefinitions;

internal static class ExampleDefinitionGate
{
    private const string InvisibleFileName = "00-invisibledragon-material-construction.gh";
    private const string SimpleFileName = "10-simpledragon-material-construction.gh";
    private const string MaterialGroupName = "1. Material Inputs";
    private const string AssemblyGroupName = "2. Construction Assembly";
    private const string ResultGroupName = "3. Calculated Result";
    private const string AssemblyNoteText =
        "Combine material and thickness into a layer.\nAdd the layer to the construction.";
    private const string ResultNoteText = "Read the calculated U-value.";
    private static readonly System.Drawing.Color MaterialGroupColour =
        System.Drawing.Color.FromArgb(255, 221, 235, 250);
    private static readonly System.Drawing.Color AssemblyGroupColour =
        System.Drawing.Color.FromArgb(255, 225, 243, 226);
    private static readonly System.Drawing.Color ResultGroupColour =
        System.Drawing.Color.FromArgb(255, 221, 242, 241);
    private static readonly DefinitionSpec[] Definitions =
    {
        new(
            "InvisibleDragon",
            InvisibleFileName,
            new Guid("dca742da-0ac5-4520-8022-97f98974dfea"),
            new Guid("d15984d5-cd3f-4798-a67c-73138b54859e"),
            new Guid("6d5a9b54-8a9e-4c95-91df-469e21a783c9"),
            "Dragons.InvisibleDragon.Grasshopper.Components.OpaqueMaterialComponent",
            "Dragons.InvisibleDragon.Grasshopper.Components.ConstructionLayerComponent",
            "Dragons.InvisibleDragon.Grasshopper.Components.LayeredConstructionComponent",
            "Dragons.InvisibleDragon.Grasshopper.Types.DragonConstructionGoo",
            new Guid("01000000-0000-4000-8000-000000000001"),
            new Guid("01000000-0000-4000-8000-000000000002"),
            new Guid("01000000-0000-4000-8000-000000000005"),
            new Guid("01000000-0000-4000-8000-000000000003"),
            new Guid("01000000-0000-4000-8000-000000000004"),
            new Guid("01000000-0000-4000-8000-000000000006"),
            new Guid("01000000-0000-4000-8000-000000000007"),
            new Guid("01000000-0000-4000-8000-000000000008"),
            new Guid("01000000-0000-4000-8000-000000000009"),
            new Guid("01000000-0000-4000-8000-000000000010"),
            new Guid("01000000-0000-4000-8000-000000000011")),
        new(
            "SimpleDragon",
            SimpleFileName,
            new Guid("fee586e8-692c-407e-a803-d5c43f3c7222"),
            new Guid("b97da4a1-7b1c-472a-a4b0-83603e202c2b"),
            new Guid("3e1fa67f-dbb2-4c19-b54b-226c295f5751"),
            "Dragons.SimpleDragon.Grasshopper.Components.SimpleDragonMaterialComponent",
            "Dragons.SimpleDragon.Grasshopper.Components.SimpleDragonSurfaceConstructionLayerComponent",
            "Dragons.SimpleDragon.Grasshopper.Components.SimpleDragonSurfaceConstructionComponent",
            "Dragons.SimpleDragon.Grasshopper.Types.SimpleDragonSurfaceConstructionGoo",
            new Guid("02000000-0000-4000-8000-000000000001"),
            new Guid("02000000-0000-4000-8000-000000000002"),
            new Guid("02000000-0000-4000-8000-000000000005"),
            new Guid("02000000-0000-4000-8000-000000000003"),
            new Guid("02000000-0000-4000-8000-000000000004"),
            new Guid("02000000-0000-4000-8000-000000000006"),
            new Guid("02000000-0000-4000-8000-000000000007"),
            new Guid("02000000-0000-4000-8000-000000000008"),
            new Guid("02000000-0000-4000-8000-000000000009"),
            new Guid("02000000-0000-4000-8000-000000000010"),
            new Guid("02000000-0000-4000-8000-000000000011"))
    };

    internal static void RestrictExternalLibraries(IReadOnlyList<string> pluginPaths)
    {
        MethodInfo method = typeof(GH_ComponentServer).GetMethod(
            "SetExternalGHAs",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(IEnumerable<string>) },
            modifiers: null)
            ?? throw new MissingMethodException(typeof(GH_ComponentServer).FullName, "SetExternalGHAs");
        method.Invoke(null, new object[] { pluginPaths });
    }

    internal static ExampleHostSummary Run(
        ExampleHostInputs inputs,
        string host,
        string rhinoVersion)
    {
        string repositoryRoot = Directory.GetParent(Path.GetFullPath(inputs.ExamplesRoot))?.FullName
            ?? throw new InvalidOperationException("The repository root could not be resolved from examples.");
        string workingDirectory = Path.GetFullPath(Environment.CurrentDirectory);
        Require(
            !IsSameOrDescendant(workingDirectory, repositoryRoot),
            "The example host must run outside the repository so document-relative paths are tested independently "
                + "of the process working directory.");

        if (inputs.Action == ExampleHostAction.Generate
            && !string.Equals(host, "Rhino7", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Canonical example generation is restricted to the oldest supported host, Rhino 7.");
        }

        RegisterDragonLibraries(inputs.PluginPaths);
        IReadOnlyList<ExampleBuildingModelResult> models = ExampleBuildingModels.Run(inputs);
        var results = new List<ExampleDefinitionResult>();
        foreach (DefinitionSpec definition in Definitions)
        {
            results.Add(inputs.Action == ExampleHostAction.Generate
                ? Generate(definition, inputs)
                : Validate(definition, inputs));
        }

        results.AddRange(AdvancedExampleDefinitions.Run(inputs));

        var summary = new ExampleHostSummary
        {
            Host = host,
            RhinoVersion = rhinoVersion,
            GrasshopperVersion = typeof(Instances).Assembly.GetName().Version?.ToString() ?? "unknown",
            Action = inputs.Action.ToString(),
            WorkingDirectory = workingDirectory,
            Definitions = results.ToArray(),
            Models = models.ToArray()
        };
        summary.Write(Path.Combine(inputs.OutputDirectory, "summary.json"));
        Console.WriteLine(
            $"Verified {results.Count} real Grasshopper definitions and {models.Count} Rhino models in {host} "
            + $"({inputs.Action.ToString().ToLowerInvariant()}).");
        return summary;
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

    private static ExampleDefinitionResult Generate(
        DefinitionSpec definition,
        ExampleHostInputs inputs)
    {
        string candidateDirectory = Path.Combine(inputs.OutputDirectory, "generated");
        Directory.CreateDirectory(candidateDirectory);
        string candidatePath = Path.Combine(candidateDirectory, definition.FileName);
        GH_Document document = CreateDefinition(definition);
        ValidateGraph(document, definition);
        Save(document, candidatePath);
        CanonicalExamplePublisher.ValidateGrasshopperIdentity(candidatePath, definition.Product);

        GH_Document candidate = Open(candidatePath);
        ValidationFacts candidateFacts = ValidateGraph(candidate, definition);
        string canonicalPath = Path.Combine(inputs.ExamplesRoot, definition.FileName);
        Directory.CreateDirectory(inputs.ExamplesRoot);
        CanonicalExamplePublisher.Publish(
            candidatePath,
            canonicalPath,
            inputs.OutputDirectory,
            path =>
            {
                CanonicalExamplePublisher.ValidateGrasshopperIdentity(path, definition.Product);
                ValidateGraph(Open(path), definition);
            });
        CanonicalExamplePublisher.ValidateGrasshopperIdentity(canonicalPath, definition.Product);
        GH_Document canonical = Open(canonicalPath);
        ValidationFacts canonicalFacts = ValidateGraph(canonical, definition);
        Require(
            candidateFacts.ObjectCount == canonicalFacts.ObjectCount
                && candidateFacts.WireCount == canonicalFacts.WireCount,
            $"The copied {definition.Product} example does not match its validated candidate.");
        return Result(definition, canonicalPath, canonicalFacts, generated: true);
    }

    private static ExampleDefinitionResult Validate(
        DefinitionSpec definition,
        ExampleHostInputs inputs)
    {
        string canonicalPath = Path.Combine(inputs.ExamplesRoot, definition.FileName);
        if (!File.Exists(canonicalPath))
        {
            throw new FileNotFoundException(
                $"The tracked {definition.Product} example is absent. Run with -Generate first.",
                canonicalPath);
        }

        CanonicalExamplePublisher.ValidateGrasshopperIdentity(canonicalPath, definition.Product);
        GH_Document canonical = Open(canonicalPath);
        ValidationFacts facts = ValidateGraph(canonical, definition);
        string roundTripDirectory = Path.Combine(inputs.OutputDirectory, "roundtrip");
        Directory.CreateDirectory(roundTripDirectory);
        string roundTripPath = Path.Combine(roundTripDirectory, definition.FileName);
        Save(canonical, roundTripPath);
        CanonicalExamplePublisher.ValidateGrasshopperIdentity(roundTripPath, definition.Product);
        GH_Document roundTrip = Open(roundTripPath);
        ValidationFacts reopenedFacts = ValidateGraph(roundTrip, definition);
        Require(
            facts.ObjectCount == reopenedFacts.ObjectCount && facts.WireCount == reopenedFacts.WireCount,
            $"The {definition.Product} example changed structure during the {inputs.Action} round trip.");
        return Result(definition, canonicalPath, reopenedFacts, generated: false);
    }

    private static GH_Document CreateDefinition(DefinitionSpec definition)
    {
        GH_ComponentServer server = Instances.ComponentServer;
        GH_Component material = EmitComponent(server, definition.MaterialComponentGuid, definition.MaterialType);
        GH_Component layer = EmitComponent(server, definition.LayerComponentGuid, definition.LayerType);
        GH_Component construction = EmitComponent(
            server,
            definition.ConstructionComponentGuid,
            definition.ConstructionType);
        var thickness = new GH_NumberSlider
        {
            NickName = "Thickness (m)"
        };
        thickness.Slider.Minimum = 0.01m;
        thickness.Slider.Maximum = 1.00m;
        thickness.Slider.DecimalPlaces = 3;
        thickness.Slider.Value = 0.100m;
        var result = new GH_Panel
        {
            NickName = "U-Value"
        };
        var materialNote = new GH_Scribble
        {
            Text = MaterialNoteText(definition),
            Font = GH_FontServer.Large,
        };
        var assemblyNote = new GH_Scribble
        {
            Text = AssemblyNoteText,
            Font = GH_FontServer.Large,
        };
        var resultNote = new GH_Scribble
        {
            Text = ResultNoteText,
            Font = GH_FontServer.Large,
        };
        GH_Group materialGroup = CreateGroup(
            MaterialGroupName,
            MaterialGroupColour);
        GH_Group assemblyGroup = CreateGroup(
            AssemblyGroupName,
            AssemblyGroupColour);
        GH_Group resultGroup = CreateGroup(
            ResultGroupName,
            ResultGroupColour);

        var document = new GH_Document();
        Add(document, materialNote, definition.MaterialNoteInstanceGuid, new System.Drawing.PointF(60, 40));
        Add(document, material, definition.MaterialInstanceGuid, new System.Drawing.PointF(80, 120));
        Add(document, thickness, definition.ThicknessInstanceGuid, new System.Drawing.PointF(100, 260));
        Add(document, assemblyNote, definition.AssemblyNoteInstanceGuid, new System.Drawing.PointF(370, 40));
        Add(document, layer, definition.LayerInstanceGuid, new System.Drawing.PointF(390, 150));
        Add(document, construction, definition.ConstructionInstanceGuid, new System.Drawing.PointF(700, 150));
        Add(document, resultNote, definition.ResultNoteInstanceGuid, new System.Drawing.PointF(1010, 100));
        Add(document, result, definition.PanelInstanceGuid, new System.Drawing.PointF(1030, 190));
        layer.Params.Input[0].AddSource(material.Params.Output[0]);
        layer.Params.Input[1].AddSource(thickness);
        construction.Params.Input[1].AddSource(layer.Params.Output[0]);
        result.AddSource(construction.Params.Output[1]);
        AddGroup(
            document,
            materialGroup,
            definition.MaterialGroupInstanceGuid,
            definition.MaterialNoteInstanceGuid,
            definition.MaterialInstanceGuid,
            definition.ThicknessInstanceGuid);
        AddGroup(
            document,
            assemblyGroup,
            definition.AssemblyGroupInstanceGuid,
            definition.AssemblyNoteInstanceGuid,
            definition.LayerInstanceGuid,
            definition.ConstructionInstanceGuid);
        AddGroup(
            document,
            resultGroup,
            definition.ResultGroupInstanceGuid,
            definition.ResultNoteInstanceGuid,
            definition.PanelInstanceGuid);
        return document;
    }

    private static ValidationFacts ValidateGraph(GH_Document document, DefinitionSpec definition)
    {
        Require(document.ObjectCount == 11, $"{definition.Product} example must contain exactly eleven objects.");
        GH_Component material = RequireObject<GH_Component>(document, definition.MaterialInstanceGuid, "material");
        GH_Component layer = RequireObject<GH_Component>(document, definition.LayerInstanceGuid, "construction layer");
        GH_Component construction = RequireObject<GH_Component>(
            document,
            definition.ConstructionInstanceGuid,
            "construction");
        GH_NumberSlider thickness = RequireObject<GH_NumberSlider>(
            document,
            definition.ThicknessInstanceGuid,
            "thickness slider");
        GH_Panel panel = RequireObject<GH_Panel>(document, definition.PanelInstanceGuid, "result panel");
        GH_Scribble materialNote = RequireNote(
            document,
            definition.MaterialNoteInstanceGuid,
            MaterialNoteText(definition),
            "material note");
        GH_Scribble assemblyNote = RequireNote(
            document,
            definition.AssemblyNoteInstanceGuid,
            AssemblyNoteText,
            "assembly note");
        GH_Scribble resultNote = RequireNote(
            document,
            definition.ResultNoteInstanceGuid,
            ResultNoteText,
            "result note");
        RequirePivot(materialNote, 60, 40, definition.Product + " material note");
        RequirePivot(material, 80, 120, definition.Product + " material component");
        RequirePivot(thickness, 100, 260, definition.Product + " thickness slider");
        RequirePivot(assemblyNote, 370, 40, definition.Product + " assembly note");
        RequirePivot(layer, 390, 150, definition.Product + " layer component");
        RequirePivot(construction, 700, 150, definition.Product + " construction component");
        RequirePivot(resultNote, 1010, 100, definition.Product + " result note");
        RequirePivot(panel, 1030, 190, definition.Product + " result panel");
        GH_Group materialGroup = RequireGroup(
            document,
            definition.MaterialGroupInstanceGuid,
            MaterialGroupName,
            MaterialGroupColour,
            new[]
            {
                definition.MaterialNoteInstanceGuid,
                definition.MaterialInstanceGuid,
                definition.ThicknessInstanceGuid,
            });
        GH_Group assemblyGroup = RequireGroup(
            document,
            definition.AssemblyGroupInstanceGuid,
            AssemblyGroupName,
            AssemblyGroupColour,
            new[]
            {
                definition.AssemblyNoteInstanceGuid,
                definition.LayerInstanceGuid,
                definition.ConstructionInstanceGuid,
            });
        GH_Group resultGroup = RequireGroup(
            document,
            definition.ResultGroupInstanceGuid,
            ResultGroupName,
            ResultGroupColour,
            new[]
            {
                definition.ResultNoteInstanceGuid,
                definition.PanelInstanceGuid,
            });
        RequireExclusiveGrouping(
            document,
            definition.Product,
            new[] { materialGroup, assemblyGroup, resultGroup });
        Require(
            material.ComponentGuid == definition.MaterialComponentGuid
                && string.Equals(material.GetType().FullName, definition.MaterialType, StringComparison.Ordinal),
            $"{definition.Product} material component identity changed.");
        Require(
            layer.ComponentGuid == definition.LayerComponentGuid
                && string.Equals(layer.GetType().FullName, definition.LayerType, StringComparison.Ordinal),
            $"{definition.Product} construction-layer component identity changed.");
        Require(
            construction.ComponentGuid == definition.ConstructionComponentGuid
                && string.Equals(construction.GetType().FullName, definition.ConstructionType, StringComparison.Ordinal),
            $"{definition.Product} construction component identity changed.");
        Require(layer.Params.Input.Count >= 2, $"{definition.Product} construction-layer inputs changed.");
        Require(layer.Params.Output.Count >= 1, $"{definition.Product} construction-layer outputs changed.");
        Require(construction.Params.Input.Count >= 2, $"{definition.Product} construction inputs changed.");
        Require(construction.Params.Output.Count >= 2, $"{definition.Product} construction outputs changed.");
        RequireSingleSource(
            layer.Params.Input[0],
            material.Params.Output[0].InstanceGuid,
            $"{definition.Product} material-to-layer wire");
        RequireSingleSource(
            layer.Params.Input[1],
            thickness.InstanceGuid,
            $"{definition.Product} thickness-to-layer wire");
        RequireSingleSource(
            construction.Params.Input[1],
            layer.Params.Output[0].InstanceGuid,
            $"{definition.Product} layer-to-construction wire");
        RequireSingleSource(
            panel,
            construction.Params.Output[1].InstanceGuid,
            $"{definition.Product} U-value-to-panel wire");
        RequireLeftToRight(material, layer, definition.Product + " material-to-layer wire");
        RequireLeftToRight(thickness, layer, definition.Product + " thickness-to-layer wire");
        RequireLeftToRight(layer, construction, definition.Product + " layer-to-construction wire");
        RequireLeftToRight(construction, panel, definition.Product + " construction-to-result wire");
        RequirePortLeftToRight(
            material.Params.Output[0],
            layer.Params.Input[0],
            definition.Product + " material-to-layer ports");
        RequirePortLeftToRight(
            thickness,
            layer.Params.Input[1],
            definition.Product + " thickness-to-layer ports");
        RequirePortLeftToRight(
            layer.Params.Output[0],
            construction.Params.Input[1],
            definition.Product + " layer-to-construction ports");
        RequirePortLeftToRight(
            construction.Params.Output[1],
            panel,
            definition.Product + " construction-to-result ports");
        int actualWireCount = document.Objects.Sum(value => value switch
        {
            GH_Component component => component.Params.Input.Sum(input => input.SourceCount),
            IGH_Param parameter => parameter.SourceCount,
            _ => 0,
        });
        Require(actualWireCount == 4, $"{definition.Product} starter wire count changed.");

        GH_Document.EnableSolutions = true;
        document.Enabled = true;
        document.NewSolution(true, GH_SolutionMode.Silent);
        foreach (GH_ActiveObject active in document.Objects.OfType<GH_ActiveObject>())
        {
            string[] errors = active.RuntimeMessages(GH_RuntimeMessageLevel.Error).ToArray();
            Require(
                errors.Length == 0,
                $"{definition.Product} object {active.NickName} reported errors: {string.Join(" | ", errors)}");
        }

        object constructionValue = construction.Params.Output[0]
            .VolatileData
            .AllData(true)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"{definition.Product} construction produced no typed value. "
                + $"DocumentEnabled={document.Enabled}; GlobalSolutions={GH_Document.EnableSolutions}; "
                + $"SolutionState={document.SolutionState}; MaterialPhase={material.Phase}; "
                + $"LayerPhase={layer.Phase}; "
                + $"ConstructionPhase={construction.Phase}; MaterialOutput="
                + $"{material.Params.Output[0].VolatileData.DataCount}; ThicknessOutput="
                + $"{thickness.VolatileData.DataCount}; LayerMaterialInput="
                + $"{layer.Params.Input[0].VolatileData.DataCount}; LayerThicknessInput="
                + $"{layer.Params.Input[1].VolatileData.DataCount}; ConstructionLayerInput="
                + $"{construction.Params.Input[1].VolatileData.DataCount}.");
        Require(
            string.Equals(constructionValue.GetType().FullName, definition.OutputGooType, StringComparison.Ordinal),
            $"{definition.Product} construction produced {constructionValue.GetType().FullName} "
                + $"instead of {definition.OutputGooType}.");
        Require(
            construction.Params.Output[1].VolatileData.DataCount == 1,
            $"{definition.Product} construction produced no U-value.");
        Require(panel.VolatileData.DataCount == 1, $"{definition.Product} panel received no U-value.");
        return new ValidationFacts(document.ObjectCount, actualWireCount, definition.OutputGooType);
    }

    private static void RegisterDragonLibraries(IReadOnlyList<string> pluginPaths)
    {
        GH_ComponentServer server = Instances.ComponentServer;
        foreach (string path in pluginPaths)
        {
            Assembly assembly = Assembly.LoadFrom(path);
            string? product = Definitions
                .Where(definition => string.Equals(
                    Path.GetFileName(path),
                    $"Dragons.{definition.Product}.GH.gha",
                    StringComparison.OrdinalIgnoreCase))
                .Select(definition => definition.Product)
                .Distinct(StringComparer.Ordinal)
                .SingleOrDefault();
            Guid[] expected = Definitions
                .Where(definition => string.Equals(definition.Product, product, StringComparison.Ordinal))
                .SelectMany(definition => new[]
                {
                    definition.MaterialComponentGuid,
                    definition.ConstructionComponentGuid
                })
                .Concat(product is null
                    ? Array.Empty<Guid>()
                    : AdvancedExampleDefinitions.ComponentIds(product))
                .Distinct()
                .ToArray();
            if (expected.Length == 0)
            {
                throw new InvalidOperationException("No example component catalog matches " + path + ".");
            }

            if (expected.Any(id => server.EmitObjectProxy(id) is null))
            {
                MethodInfo parser = server.GetType().GetMethod(
                    "ParseGHA",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(Assembly), typeof(string) },
                    modifiers: null)
                    ?? throw new MissingMethodException(server.GetType().FullName, "ParseGHA(Assembly, string)");
                parser.Invoke(server, new object[] { assembly, path });
            }

            foreach (Guid id in expected)
            {
                Require(server.EmitObjectProxy(id) is not null, $"Grasshopper did not register component {id}.");
            }
        }
    }

    private static GH_Component EmitComponent(GH_ComponentServer server, Guid id, string expectedType)
    {
        IGH_DocumentObject value = server.EmitObject(id)
            ?? throw new InvalidOperationException($"Grasshopper could not emit component {id}.");
        Require(value is GH_Component, $"Grasshopper object {id} is not a component.");
        Require(
            string.Equals(value.GetType().FullName, expectedType, StringComparison.Ordinal),
            $"Grasshopper emitted {value.GetType().FullName} instead of {expectedType}.");
        return (GH_Component)value;
    }

    private static string MaterialNoteText(DefinitionSpec definition)
    {
        return definition.Product + " starter: choose an opaque material.\nSet its layer thickness.";
    }

    private static GH_Group CreateGroup(string name, System.Drawing.Color colour)
    {
        return new GH_Group
        {
            NickName = name,
            Border = GH_GroupBorder.Box,
            Colour = colour,
        };
    }

    private static void Add(
        GH_Document document,
        IGH_DocumentObject value,
        Guid instanceGuid,
        System.Drawing.PointF pivot)
    {
        value.NewInstanceGuid(instanceGuid);
        value.CreateAttributes();
        value.Attributes.Pivot = pivot;
        Require(
            document.AddObject(value, update: false, index: document.ObjectCount),
            $"Grasshopper refused to add {value.GetType().FullName} to the example document.");
        value.Attributes.ExpireLayout();
        value.Attributes.PerformLayout();
    }

    private static void AddGroup(
        GH_Document document,
        GH_Group group,
        Guid instanceGuid,
        params Guid[] memberGuids)
    {
        Require(memberGuids.Length >= 2, "A native example group must contain at least two objects.");
        Require(
            memberGuids.Distinct().Count() == memberGuids.Length,
            "A native example group cannot list the same object more than once.");
        Add(document, group, instanceGuid, System.Drawing.PointF.Empty);
        foreach (Guid memberGuid in memberGuids)
        {
            group.AddObject(memberGuid);
        }

        group.ExpireCaches();
        group.Attributes.ExpireLayout();
        group.Attributes.PerformLayout();
    }

    private static T RequireObject<T>(GH_Document document, Guid instanceGuid, string label)
        where T : class, IGH_DocumentObject
    {
        IGH_DocumentObject value = document.FindObject(instanceGuid, topLevelOnly: true)
            ?? throw new InvalidOperationException($"Reopened document lost the {label} object.");
        return value as T
            ?? throw new InvalidOperationException(
                $"The {label} object reopened as {value.GetType().FullName}, not {typeof(T).FullName}.");
    }

    private static GH_Scribble RequireNote(
        GH_Document document,
        Guid instanceGuid,
        string expectedText,
        string label)
    {
        GH_Scribble note = RequireObject<GH_Scribble>(document, instanceGuid, label);
        Require(
            string.Equals(note.Text, expectedText, StringComparison.Ordinal),
            $"The {label} text changed after reopening the definition.");
        Require(
            string.Equals(note.Font.Name, GH_FontServer.Large.Name, StringComparison.Ordinal)
                && Math.Abs(note.Font.Size - GH_FontServer.Large.Size) <= 0.1f
                && note.Font.Style == GH_FontServer.Large.Style,
            $"The {label} font changed after reopening the definition.");
        Require(
            note.Attributes.Bounds.Width > 20 && note.Attributes.Bounds.Height > 10,
            $"The {label} has invalid display bounds.");
        return note;
    }

    private static GH_Group RequireGroup(
        GH_Document document,
        Guid instanceGuid,
        string expectedName,
        System.Drawing.Color expectedColour,
        IReadOnlyCollection<Guid> expectedMemberGuids)
    {
        GH_Group group = RequireObject<GH_Group>(document, instanceGuid, expectedName + " group");
        Require(
            string.Equals(group.NickName, expectedName, StringComparison.Ordinal),
            $"The {expectedName} group name changed after reopening the definition.");
        Require(
            group.Border == GH_GroupBorder.Box && group.Colour.ToArgb() == expectedColour.ToArgb(),
            $"The {expectedName} group appearance changed after reopening the definition.");
        Require(
            group.ObjectIDs.Count == expectedMemberGuids.Count
                && group.ObjectIDs.ToHashSet().SetEquals(expectedMemberGuids),
            $"The {expectedName} group membership changed after reopening the definition.");
        Require(
            group.Attributes.Bounds.Width > 20 && group.Attributes.Bounds.Height > 20,
            $"The {expectedName} group has invalid display bounds.");
        IGH_DocumentObject[] members = expectedMemberGuids
            .Select(memberGuid => document.FindObject(memberGuid, topLevelOnly: true)
                ?? throw new InvalidOperationException($"The {expectedName} group lost a member."))
            .ToArray();
        for (int first = 0; first < members.Length; first++)
        {
            for (int second = first + 1; second < members.Length; second++)
            {
                System.Drawing.RectangleF intersection = System.Drawing.RectangleF.Intersect(
                    members[first].Attributes.Bounds,
                    members[second].Attributes.Bounds);
                Require(
                    intersection.Width <= 1 || intersection.Height <= 1,
                    $"Objects overlap inside the {expectedName} group.");
            }
        }

        return group;
    }

    private static void RequireExclusiveGrouping(
        GH_Document document,
        string product,
        IReadOnlyCollection<GH_Group> groups)
    {
        Guid[] groupableObjectGuids = document.Objects
            .Where(value => value is not GH_Group)
            .Select(value => value.InstanceGuid)
            .ToArray();
        Guid[] groupedObjectGuids = groups
            .SelectMany(group => group.ObjectIDs)
            .ToArray();
        Require(
            groupedObjectGuids.Length == groupableObjectGuids.Length
                && groupedObjectGuids.ToHashSet().SetEquals(groupableObjectGuids),
            $"Every {product} functional object and canvas note must belong to exactly one native group.");
        GH_Group[] displayedGroups = groups.ToArray();
        for (int first = 0; first < displayedGroups.Length; first++)
        {
            for (int second = first + 1; second < displayedGroups.Length; second++)
            {
                System.Drawing.RectangleF intersection = System.Drawing.RectangleF.Intersect(
                    displayedGroups[first].Attributes.Bounds,
                    displayedGroups[second].Attributes.Bounds);
                Require(
                    intersection.Width <= 1 || intersection.Height <= 1,
                    $"The {product} groups '{displayedGroups[first].NickName}' and "
                        + $"'{displayedGroups[second].NickName}' overlap.");
            }
        }
    }

    private static void RequireSingleSource(IGH_Param target, Guid sourceGuid, string label)
    {
        Require(target.SourceCount == 1, $"{label} must have exactly one source.");
        Require(target.Sources[0].InstanceGuid == sourceGuid, $"{label} points to the wrong source.");
    }

    private static void RequireLeftToRight(
        IGH_DocumentObject source,
        IGH_DocumentObject target,
        string label)
    {
        Require(
            source.Attributes.Pivot.X < target.Attributes.Pivot.X,
            $"{label} must flow from left to right on the example canvas.");
    }

    private static void RequirePivot(
        IGH_DocumentObject value,
        float expectedX,
        float expectedY,
        string label)
    {
        Require(
            Math.Abs(value.Attributes.Pivot.X - expectedX) <= 0.1f
                && Math.Abs(value.Attributes.Pivot.Y - expectedY) <= 0.1f,
            $"{label} canvas position changed after reopening the definition.");
    }

    private static void RequirePortLeftToRight(IGH_Param source, IGH_Param target, string label)
    {
        Require(
            source.Attributes.Pivot.X < target.Attributes.Pivot.X,
            $"{label} must flow from left to right without a reverse hook.");
    }

    private static void Save(GH_Document document, string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var writer = new GH_DocumentIO(document);
        Require(writer.SaveQuiet(path), "Grasshopper failed to save " + path + ".");
        Require(File.Exists(path), "Grasshopper reported a save but the file is absent: " + path + ".");
    }

    private static GH_Document Open(string path)
    {
        var reader = new GH_DocumentIO();
        Require(reader.Open(path), "Grasshopper failed to open " + path + ".");
        return reader.Document
            ?? throw new InvalidOperationException("Grasshopper opened a document without content: " + path + ".");
    }

    private static ExampleDefinitionResult Result(
        DefinitionSpec definition,
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
            Generated = generated
        };
    }

    private static string ComputeSha256(string path)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return string.Concat(sha256.ComputeHash(stream).Select(value =>
            value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class DefinitionSpec
    {
        internal DefinitionSpec(
            string product,
            string fileName,
            Guid materialComponentGuid,
            Guid layerComponentGuid,
            Guid constructionComponentGuid,
            string materialType,
            string layerType,
            string constructionType,
            string outputGooType,
            Guid materialInstanceGuid,
            Guid thicknessInstanceGuid,
            Guid layerInstanceGuid,
            Guid constructionInstanceGuid,
            Guid panelInstanceGuid,
            Guid materialNoteInstanceGuid,
            Guid assemblyNoteInstanceGuid,
            Guid resultNoteInstanceGuid,
            Guid materialGroupInstanceGuid,
            Guid assemblyGroupInstanceGuid,
            Guid resultGroupInstanceGuid)
        {
            Product = product;
            FileName = fileName;
            MaterialComponentGuid = materialComponentGuid;
            LayerComponentGuid = layerComponentGuid;
            ConstructionComponentGuid = constructionComponentGuid;
            MaterialType = materialType;
            LayerType = layerType;
            ConstructionType = constructionType;
            OutputGooType = outputGooType;
            MaterialInstanceGuid = materialInstanceGuid;
            ThicknessInstanceGuid = thicknessInstanceGuid;
            LayerInstanceGuid = layerInstanceGuid;
            ConstructionInstanceGuid = constructionInstanceGuid;
            PanelInstanceGuid = panelInstanceGuid;
            MaterialNoteInstanceGuid = materialNoteInstanceGuid;
            AssemblyNoteInstanceGuid = assemblyNoteInstanceGuid;
            ResultNoteInstanceGuid = resultNoteInstanceGuid;
            MaterialGroupInstanceGuid = materialGroupInstanceGuid;
            AssemblyGroupInstanceGuid = assemblyGroupInstanceGuid;
            ResultGroupInstanceGuid = resultGroupInstanceGuid;
        }

        internal string Product { get; }

        internal string FileName { get; }

        internal Guid MaterialComponentGuid { get; }

        internal Guid LayerComponentGuid { get; }

        internal Guid ConstructionComponentGuid { get; }

        internal string MaterialType { get; }

        internal string LayerType { get; }

        internal string ConstructionType { get; }

        internal string OutputGooType { get; }

        internal Guid MaterialInstanceGuid { get; }

        internal Guid ThicknessInstanceGuid { get; }

        internal Guid LayerInstanceGuid { get; }

        internal Guid ConstructionInstanceGuid { get; }

        internal Guid PanelInstanceGuid { get; }

        internal Guid MaterialNoteInstanceGuid { get; }

        internal Guid AssemblyNoteInstanceGuid { get; }

        internal Guid ResultNoteInstanceGuid { get; }

        internal Guid MaterialGroupInstanceGuid { get; }

        internal Guid AssemblyGroupInstanceGuid { get; }

        internal Guid ResultGroupInstanceGuid { get; }
    }

    private sealed class ValidationFacts
    {
        internal ValidationFacts(int objectCount, int wireCount, string outputGooType)
        {
            ObjectCount = objectCount;
            WireCount = wireCount;
            OutputGooType = outputGooType;
        }

        internal int ObjectCount { get; }

        internal int WireCount { get; }

        internal string OutputGooType { get; }
    }
}

[DataContract]
internal sealed class ExampleHostSummary
{
    [DataMember(Name = "schema", Order = 1)]
    public string Schema { get; set; } = "dragons-grasshopper.examples.v3";

    [DataMember(Name = "host", Order = 2)]
    public string Host { get; set; } = string.Empty;

    [DataMember(Name = "rhinoVersion", Order = 3)]
    public string RhinoVersion { get; set; } = string.Empty;

    [DataMember(Name = "grasshopperVersion", Order = 4)]
    public string GrasshopperVersion { get; set; } = string.Empty;

    [DataMember(Name = "action", Order = 5)]
    public string Action { get; set; } = string.Empty;

    [DataMember(Name = "workingDirectory", Order = 6)]
    public string WorkingDirectory { get; set; } = string.Empty;

    [DataMember(Name = "definitions", Order = 7)]
    public ExampleDefinitionResult[] Definitions { get; set; } = Array.Empty<ExampleDefinitionResult>();

    [DataMember(Name = "models", Order = 8)]
    public ExampleBuildingModelResult[] Models { get; set; } = Array.Empty<ExampleBuildingModelResult>();

    internal void Write(string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        var serializer = new DataContractJsonSerializer(typeof(ExampleHostSummary));
        serializer.WriteObject(stream, this);
    }
}

[DataContract]
internal sealed class ExampleDefinitionResult
{
    [DataMember(Name = "product", Order = 1)]
    public string Product { get; set; } = string.Empty;

    [DataMember(Name = "fileName", Order = 2)]
    public string FileName { get; set; } = string.Empty;

    [DataMember(Name = "canonicalPath", Order = 3)]
    public string CanonicalPath { get; set; } = string.Empty;

    [DataMember(Name = "sha256", Order = 4)]
    public string Sha256 { get; set; } = string.Empty;

    [DataMember(Name = "objectCount", Order = 5)]
    public int ObjectCount { get; set; }

    [DataMember(Name = "wireCount", Order = 6)]
    public int WireCount { get; set; }

    [DataMember(Name = "outputGooType", Order = 7)]
    public string OutputGooType { get; set; } = string.Empty;

    [DataMember(Name = "generated", Order = 8)]
    public bool Generated { get; set; }

    [DataMember(Name = "runtimeGateStatus", Order = 9)]
    public string RuntimeGateStatus { get; set; } = "not-applicable";

    [DataMember(Name = "runtimeGateReason", Order = 10)]
    public string RuntimeGateReason { get; set; } = "This definition has no executable EnergyPlus workflow.";

    [DataMember(Name = "runtimeExecuted", Order = 11)]
    public bool RuntimeExecuted { get; set; }

    [DataMember(Name = "runtimeState", Order = 12)]
    public string RuntimeState { get; set; } = "Not Run";

    [DataMember(Name = "runtimeResultVerified", Order = 13)]
    public bool RuntimeResultVerified { get; set; }

    [DataMember(Name = "runtimeCsvVerified", Order = 14)]
    public bool RuntimeCsvVerified { get; set; }

    [DataMember(Name = "runtimeCacheVerified", Order = 15)]
    public bool RuntimeCacheVerified { get; set; }

    [DataMember(Name = "runtimeCancellationVerified", Order = 16)]
    public bool RuntimeCancellationVerified { get; set; }

    [DataMember(Name = "runtimeBatchVerified", Order = 17)]
    public bool RuntimeBatchVerified { get; set; }

    [DataMember(Name = "runtimeFirstRunState", Order = 18)]
    public string RuntimeFirstRunState { get; set; } = "Not Run";

    [DataMember(Name = "runtimeCachedRunState", Order = 19)]
    public string RuntimeCachedRunState { get; set; } = "Not Run";

    [DataMember(Name = "runtimeCancellationState", Order = 20)]
    public string RuntimeCancellationState { get; set; } = "Not Run";

    [DataMember(Name = "runtimeFirstBatchState", Order = 21)]
    public string RuntimeFirstBatchState { get; set; } = "Not Run";

    [DataMember(Name = "runtimeCachedBatchState", Order = 22)]
    public string RuntimeCachedBatchState { get; set; } = "Not Run";

    [DataMember(Name = "runtimeBatchCancellationState", Order = 23)]
    public string RuntimeBatchCancellationState { get; set; } = "Not Run";

    [DataMember(Name = "runtimeBatchCancellationVerified", Order = 24)]
    public bool RuntimeBatchCancellationVerified { get; set; }

    [DataMember(Name = "runtimeEvidenceDirectory", Order = 25)]
    public string RuntimeEvidenceDirectory { get; set; } = string.Empty;

    [DataMember(Name = "runtimeAnnualResult", Order = 26)]
    public double? RuntimeAnnualResult { get; set; }

    [DataMember(Name = "runtimeCsvSha256", Order = 27)]
    public string[] RuntimeCsvSha256 { get; set; } = Array.Empty<string>();

    [DataMember(Name = "runtimeBatchCombinedCsvSha256", Order = 28)]
    public string RuntimeBatchCombinedCsvSha256 { get; set; } = string.Empty;

    [DataMember(Name = "runtimeBatchManifestSha256", Order = 29)]
    public string RuntimeBatchManifestSha256 { get; set; } = string.Empty;

    [DataMember(Name = "runtimeBatchCancellationCsvSha256", Order = 30)]
    public string RuntimeBatchCancellationCsvSha256 { get; set; } = string.Empty;

    [DataMember(Name = "runtimeBatchCancellationManifestSha256", Order = 31)]
    public string RuntimeBatchCancellationManifestSha256 { get; set; } = string.Empty;
}
