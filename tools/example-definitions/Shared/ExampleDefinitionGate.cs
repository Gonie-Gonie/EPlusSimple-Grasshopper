using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace GonieGonie.Dragons.ExampleDefinitions;

internal static class ExampleDefinitionGate
{
    private const string InvisibleFileName = "00-invisibledragon-material-construction.gh";
    private const string SimpleFileName = "10-simpledragon-material-construction.gh";
    private static readonly DefinitionSpec[] Definitions =
    {
        new(
            "InvisibleDragon",
            InvisibleFileName,
            new Guid("dca742da-0ac5-4520-8022-97f98974dfea"),
            new Guid("6d5a9b54-8a9e-4c95-91df-469e21a783c9"),
            "GonieGonie.InvisibleDragon.Grasshopper.Components.OpaqueMaterialComponent",
            "GonieGonie.InvisibleDragon.Grasshopper.Components.LayeredConstructionComponent",
            "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonConstructionGoo",
            new Guid("01000000-0000-4000-8000-000000000001"),
            new Guid("01000000-0000-4000-8000-000000000002"),
            new Guid("01000000-0000-4000-8000-000000000003"),
            new Guid("01000000-0000-4000-8000-000000000004")),
        new(
            "SimpleDragon",
            SimpleFileName,
            new Guid("fee586e8-692c-407e-a803-d5c43f3c7222"),
            new Guid("3e1fa67f-dbb2-4c19-b54b-226c295f5751"),
            "GonieGonie.SimpleDragon.Grasshopper.Components.SimpleDragonMaterialComponent",
            "GonieGonie.SimpleDragon.Grasshopper.Components.SimpleDragonSurfaceConstructionComponent",
            "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonSurfaceConstructionGoo",
            new Guid("02000000-0000-4000-8000-000000000001"),
            new Guid("02000000-0000-4000-8000-000000000002"),
            new Guid("02000000-0000-4000-8000-000000000003"),
            new Guid("02000000-0000-4000-8000-000000000004"))
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

        GH_Document candidate = Open(candidatePath);
        ValidationFacts candidateFacts = ValidateGraph(candidate, definition);
        string canonicalPath = Path.Combine(inputs.ExamplesRoot, definition.FileName);
        Directory.CreateDirectory(inputs.ExamplesRoot);
        File.Copy(candidatePath, canonicalPath, overwrite: true);
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

        GH_Document canonical = Open(canonicalPath);
        ValidationFacts facts = ValidateGraph(canonical, definition);
        string roundTripDirectory = Path.Combine(inputs.OutputDirectory, "roundtrip");
        Directory.CreateDirectory(roundTripDirectory);
        string roundTripPath = Path.Combine(roundTripDirectory, definition.FileName);
        Save(canonical, roundTripPath);
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

        var document = new GH_Document();
        Add(document, material, definition.MaterialInstanceGuid, new System.Drawing.PointF(80, 100));
        Add(document, thickness, definition.ThicknessInstanceGuid, new System.Drawing.PointF(100, 260));
        Add(document, construction, definition.ConstructionInstanceGuid, new System.Drawing.PointF(400, 140));
        Add(document, result, definition.PanelInstanceGuid, new System.Drawing.PointF(720, 190));
        construction.Params.Input[1].AddSource(material.Params.Output[0]);
        construction.Params.Input[2].AddSource(thickness);
        result.AddSource(construction.Params.Output[1]);
        return document;
    }

    private static ValidationFacts ValidateGraph(GH_Document document, DefinitionSpec definition)
    {
        Require(document.ObjectCount == 4, $"{definition.Product} example must contain exactly four objects.");
        GH_Component material = RequireObject<GH_Component>(document, definition.MaterialInstanceGuid, "material");
        GH_Component construction = RequireObject<GH_Component>(
            document,
            definition.ConstructionInstanceGuid,
            "construction");
        GH_NumberSlider thickness = RequireObject<GH_NumberSlider>(
            document,
            definition.ThicknessInstanceGuid,
            "thickness slider");
        GH_Panel panel = RequireObject<GH_Panel>(document, definition.PanelInstanceGuid, "result panel");
        Require(
            material.ComponentGuid == definition.MaterialComponentGuid
                && string.Equals(material.GetType().FullName, definition.MaterialType, StringComparison.Ordinal),
            $"{definition.Product} material component identity changed.");
        Require(
            construction.ComponentGuid == definition.ConstructionComponentGuid
                && string.Equals(construction.GetType().FullName, definition.ConstructionType, StringComparison.Ordinal),
            $"{definition.Product} construction component identity changed.");
        Require(construction.Params.Input.Count >= 3, $"{definition.Product} construction inputs changed.");
        Require(construction.Params.Output.Count >= 2, $"{definition.Product} construction outputs changed.");
        RequireSingleSource(
            construction.Params.Input[1],
            material.Params.Output[0].InstanceGuid,
            $"{definition.Product} material-to-construction wire");
        RequireSingleSource(
            construction.Params.Input[2],
            thickness.InstanceGuid,
            $"{definition.Product} thickness-to-construction wire");
        RequireSingleSource(
            panel,
            construction.Params.Output[1].InstanceGuid,
            $"{definition.Product} U-value-to-panel wire");
        int actualWireCount = document.Objects.Sum(value => value switch
        {
            GH_Component component => component.Params.Input.Sum(input => input.SourceCount),
            IGH_Param parameter => parameter.SourceCount,
            _ => 0,
        });
        Require(actualWireCount == 3, $"{definition.Product} starter wire count changed.");

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
                + $"ConstructionPhase={construction.Phase}; MaterialOutput="
                + $"{material.Params.Output[0].VolatileData.DataCount}; ThicknessOutput="
                + $"{thickness.VolatileData.DataCount}; MaterialInput="
                + $"{construction.Params.Input[1].VolatileData.DataCount}; ThicknessInput="
                + $"{construction.Params.Input[2].VolatileData.DataCount}.");
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
                    $"GonieGonie.{definition.Product}.GH.gha",
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

    private static void RequireSingleSource(IGH_Param target, Guid sourceGuid, string label)
    {
        Require(target.SourceCount == 1, $"{label} must have exactly one source.");
        Require(target.Sources[0].InstanceGuid == sourceGuid, $"{label} points to the wrong source.");
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
            Guid constructionComponentGuid,
            string materialType,
            string constructionType,
            string outputGooType,
            Guid materialInstanceGuid,
            Guid thicknessInstanceGuid,
            Guid constructionInstanceGuid,
            Guid panelInstanceGuid)
        {
            Product = product;
            FileName = fileName;
            MaterialComponentGuid = materialComponentGuid;
            ConstructionComponentGuid = constructionComponentGuid;
            MaterialType = materialType;
            ConstructionType = constructionType;
            OutputGooType = outputGooType;
            MaterialInstanceGuid = materialInstanceGuid;
            ThicknessInstanceGuid = thicknessInstanceGuid;
            ConstructionInstanceGuid = constructionInstanceGuid;
            PanelInstanceGuid = panelInstanceGuid;
        }

        internal string Product { get; }

        internal string FileName { get; }

        internal Guid MaterialComponentGuid { get; }

        internal Guid ConstructionComponentGuid { get; }

        internal string MaterialType { get; }

        internal string ConstructionType { get; }

        internal string OutputGooType { get; }

        internal Guid MaterialInstanceGuid { get; }

        internal Guid ThicknessInstanceGuid { get; }

        internal Guid ConstructionInstanceGuid { get; }

        internal Guid PanelInstanceGuid { get; }
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
    public string Schema { get; set; } = "goniegonie.dragons-grasshopper.examples.v3";

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
