using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GonieGonie.Dragons.GrasshopperSmoke;

internal enum SmokeScenario
{
    InvisibleOnly,
    SimpleOnly,
    Both
}

internal sealed class SmokeHostInputs
{
    private const string ScenarioVariable = "DRAGONS_SMOKE_SCENARIO";
    private const string SourceVariable = "DRAGONS_SMOKE_SOURCE";
    private const string PluginPathsVariable = "DRAGONS_PLUGIN_PATHS";
    private const string PluginHashesVariable = "DRAGONS_PLUGIN_SHA256";
    private const string ArchivePathsVariable = "DRAGONS_PORTABLE_ARCHIVE_PATHS";
    private const string ArchiveHashesVariable = "DRAGONS_PORTABLE_ARCHIVE_SHA256";
    private const string AllowedRootsVariable = "DRAGONS_ALLOWED_PLUGIN_ROOTS";
    private const string OutputVariable = "DRAGONS_GRASSHOPPER_SMOKE_OUTPUT";
    private const string DocumentVariable = "DRAGONS_GRASSHOPPER_SMOKE_DOCUMENT";
    private const string LegacyInvisibleVariable = "DRAGONS_INVISIBLE_GHA";
    private const string LegacySimpleVariable = "DRAGONS_SIMPLE_GHA";

    private SmokeHostInputs(
        SmokeScenario scenario,
        string source,
        IReadOnlyList<string> pluginPaths,
        IReadOnlyList<SmokeArtifactProvenance> pluginArtifacts,
        IReadOnlyList<SmokeArtifactProvenance> portableArchives,
        IReadOnlyList<string> allowedPluginRoots,
        string outputDirectory,
        string? documentPath)
    {
        Scenario = scenario;
        Source = source;
        PluginPaths = pluginPaths;
        PluginArtifacts = pluginArtifacts;
        PortableArchives = portableArchives;
        AllowedPluginRoots = allowedPluginRoots;
        OutputDirectory = outputDirectory;
        DocumentPath = documentPath ?? Path.Combine(
            outputDirectory,
            "dragons-" + scenario.ToString().ToLowerInvariant() + "-host-gate.gh");
    }

    internal SmokeScenario Scenario { get; }

    internal string Source { get; }

    internal IReadOnlyList<string> PluginPaths { get; }

    internal IReadOnlyList<SmokeArtifactProvenance> PluginArtifacts { get; }

    internal IReadOnlyList<SmokeArtifactProvenance> PortableArchives { get; }

    internal IReadOnlyList<string> AllowedPluginRoots { get; }

    internal string OutputDirectory { get; }

    internal string DocumentPath { get; }

    internal string SummaryPath => Path.Combine(OutputDirectory, "summary.json");

    internal static SmokeHostInputs FromEnvironment()
    {
        var scenarioText = Environment.GetEnvironmentVariable(ScenarioVariable) ?? "Both";
        if (!Enum.TryParse(scenarioText, ignoreCase: true, out SmokeScenario scenario))
        {
            throw new InvalidOperationException(
                $"{ScenarioVariable} must be InvisibleOnly, SimpleOnly, or Both.");
        }

        string source = Environment.GetEnvironmentVariable(SourceVariable) ?? "build-output";
        if (!string.Equals(source, "build-output", StringComparison.Ordinal)
            && !string.Equals(source, "portable-package", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{SourceVariable} must be build-output or portable-package.");
        }

        var pluginPaths = ReadPathList(PluginPathsVariable);
        if (pluginPaths.Length == 0)
        {
            pluginPaths = ReadLegacyPluginPaths();
        }

        var allowedRoots = ReadPathList(AllowedRootsVariable);
        if (allowedRoots.Length == 0)
        {
            allowedRoots = pluginPaths
                .Select(path => Path.GetDirectoryName(path)!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        string output = Environment.GetEnvironmentVariable(OutputVariable)
            ?? throw new InvalidOperationException($"Environment variable {OutputVariable} is required.");
        string outputDirectory = Path.GetFullPath(output);
        string? requestedDocument = Environment.GetEnvironmentVariable(DocumentVariable);
        string? documentPath = string.IsNullOrWhiteSpace(requestedDocument)
            ? null
            : Path.GetFullPath(requestedDocument);

        ValidatePlugins(scenario, pluginPaths, allowedRoots);
        IReadOnlyList<SmokeArtifactProvenance> pluginArtifacts = ReadPluginArtifacts(pluginPaths);
        IReadOnlyList<SmokeArtifactProvenance> portableArchives = ReadArchiveArtifacts(source);
        ValidateArtifactProducts(scenario, pluginArtifacts, portableArchives, source);
        return new SmokeHostInputs(
            scenario,
            source,
            pluginPaths,
            pluginArtifacts,
            portableArchives,
            allowedRoots,
            outputDirectory,
            documentPath);
    }

    private static string[] ReadLegacyPluginPaths()
    {
        var paths = new List<string>();
        foreach (string variable in new[] { LegacyInvisibleVariable, LegacySimpleVariable })
        {
            string? value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value))
            {
                paths.Add(Path.GetFullPath(value));
            }
        }

        return paths.ToArray();
    }

    private static string[] ReadPathList(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] ReadTextList(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static List<SmokeArtifactProvenance> ReadPluginArtifacts(
        IReadOnlyList<string> pluginPaths)
    {
        string[] expectedHashes = ReadTextList(PluginHashesVariable);
        if (expectedHashes.Length != 0 && expectedHashes.Length != pluginPaths.Count)
        {
            throw new InvalidOperationException(
                $"{PluginHashesVariable} must contain one SHA-256 per requested GHA.");
        }

        var artifacts = new List<SmokeArtifactProvenance>(pluginPaths.Count);
        for (int index = 0; index < pluginPaths.Count; index++)
        {
            DragonModuleSpec spec = DragonModuleSpec.FromPluginPath(pluginPaths[index]);
            string? expectedHash = expectedHashes.Length == 0 ? null : expectedHashes[index];
            artifacts.Add(SmokeArtifactProvenance.CreateVerified(
                spec.Product,
                pluginPaths[index],
                expectedHash));
        }

        return artifacts;
    }

    private static List<SmokeArtifactProvenance> ReadArchiveArtifacts(string source)
    {
        string[] paths = ReadPathList(ArchivePathsVariable);
        string[] hashes = ReadTextList(ArchiveHashesVariable);
        if (paths.Length != hashes.Length)
        {
            throw new InvalidOperationException(
                $"{ArchivePathsVariable} and {ArchiveHashesVariable} must have the same item count.");
        }

        if (string.Equals(source, "portable-package", StringComparison.Ordinal) && paths.Length == 0)
        {
            throw new InvalidOperationException(
                "Portable-package smoke inputs must identify their source archives and SHA-256 values.");
        }

        var artifacts = new List<SmokeArtifactProvenance>(paths.Length);
        for (int index = 0; index < paths.Length; index++)
        {
            artifacts.Add(SmokeArtifactProvenance.CreateVerified(
                ProductFromArchivePath(paths[index]),
                paths[index],
                hashes[index]));
        }

        return artifacts;
    }

    private static string ProductFromArchivePath(string path)
    {
        string fileName = Path.GetFileName(path);
        if (fileName.StartsWith("invisible-dragon-", StringComparison.OrdinalIgnoreCase))
        {
            return "InvisibleDragon";
        }

        if (fileName.StartsWith("simple-dragon-", StringComparison.OrdinalIgnoreCase))
        {
            return "SimpleDragon";
        }

        throw new InvalidOperationException("Unexpected portable archive filename: " + path);
    }

    private static void ValidateArtifactProducts(
        SmokeScenario scenario,
        IReadOnlyList<SmokeArtifactProvenance> plugins,
        IReadOnlyList<SmokeArtifactProvenance> archives,
        string source)
    {
        var expectedProducts = new HashSet<string>(StringComparer.Ordinal);
        if (scenario != SmokeScenario.SimpleOnly)
        {
            expectedProducts.Add("InvisibleDragon");
        }
        if (scenario != SmokeScenario.InvisibleOnly)
        {
            expectedProducts.Add("SimpleDragon");
        }

        if (!expectedProducts.SetEquals(plugins.Select(item => item.Product)))
        {
            throw new InvalidOperationException("GHA provenance products do not match the smoke scenario.");
        }

        if (string.Equals(source, "portable-package", StringComparison.Ordinal))
        {
            if (!expectedProducts.SetEquals(archives.Select(item => item.Product)))
            {
                throw new InvalidOperationException(
                    "Portable archive provenance products do not match the smoke scenario.");
            }
        }
        else if (archives.Count != 0)
        {
            throw new InvalidOperationException(
                "Build-output smoke inputs must not claim portable archive provenance.");
        }
    }

    private static void ValidatePlugins(
        SmokeScenario scenario,
        IReadOnlyList<string> pluginPaths,
        IReadOnlyList<string> allowedRoots)
    {
        int expectedCount = scenario == SmokeScenario.Both ? 2 : 1;
        if (pluginPaths.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Scenario {scenario} requires exactly {expectedCount} GHA path(s); got {pluginPaths.Count}.");
        }

        bool hasInvisible = false;
        bool hasSimple = false;
        foreach (string pluginPath in pluginPaths)
        {
            if (!File.Exists(pluginPath))
            {
                throw new FileNotFoundException("A requested Dragon GHA does not exist.", pluginPath);
            }

            string fileName = Path.GetFileName(pluginPath);
            if (string.Equals(
                    fileName,
                    DragonModuleSpec.InvisibleGhaFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                hasInvisible = true;
            }
            else if (string.Equals(
                    fileName,
                    DragonModuleSpec.SimpleGhaFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                hasSimple = true;
            }
            else
            {
                throw new InvalidOperationException("Unexpected Dragon GHA filename: " + pluginPath);
            }

            if (!allowedRoots.Any(root => IsWithin(root, pluginPath)))
            {
                throw new InvalidOperationException(
                    "A requested Dragon GHA is outside every permitted payload root: " + pluginPath);
            }
        }

        bool expectedInvisible = scenario != SmokeScenario.SimpleOnly;
        bool expectedSimple = scenario != SmokeScenario.InvisibleOnly;
        if (hasInvisible != expectedInvisible || hasSimple != expectedSimple)
        {
            throw new InvalidOperationException(
                $"Scenario {scenario} received the wrong product GHA set.");
        }
    }

    internal static bool IsWithin(string rootPath, string candidatePath)
    {
        string root = Path.GetFullPath(rootPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string candidate = Path.GetFullPath(candidatePath);
        if (string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string prefix = root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class SmokeArtifactProvenance
{
    private SmokeArtifactProvenance(string product, string path, string sha256)
    {
        Product = product;
        Path = path;
        Sha256 = sha256;
    }

    internal string Product { get; }

    internal string Path { get; }

    internal string Sha256 { get; }

    internal void VerifyUnchanged()
    {
        string currentSha256 = ComputeSha256(Path);
        if (!string.Equals(currentSha256, Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Provenance artifact changed during the host gate: '{Path}'. "
                    + $"Expected {Sha256}, got {currentSha256}.");
        }
    }

    internal static SmokeArtifactProvenance CreateVerified(
        string product,
        string path,
        string? expectedSha256)
    {
        string fullPath = System.IO.Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("A provenance artifact does not exist.", fullPath);
        }

        string actualSha256 = ComputeSha256(fullPath);
        if (expectedSha256 is not null)
        {
            ValidateSha256(expectedSha256);
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"SHA-256 mismatch for '{fullPath}': expected {expectedSha256}, got {actualSha256}.");
            }
        }

        return new SmokeArtifactProvenance(product, fullPath, actualSha256);
    }

    private static string ComputeSha256(string path)
    {
        using var algorithm = SHA256.Create();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] hash = algorithm.ComputeHash(stream);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (byte value in hash)
        {
            builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static void ValidateSha256(string value)
    {
        if (value.Length != 64 || value.Any(character =>
                !((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F'))))
        {
            throw new InvalidOperationException("Expected SHA-256 values must contain exactly 64 hex digits.");
        }
    }
}

internal sealed class DragonModuleSpec
{
    internal const string InvisibleGhaFileName = "GonieGonie.InvisibleDragon.GH.gha";
    internal const string SimpleGhaFileName = "GonieGonie.SimpleDragon.GH.gha";

    private DragonModuleSpec(
        string product,
        string pluginPath,
        string pluginAssemblyName,
        string typesAssemblyName,
        string parameterNamespace,
        string persistenceParameterType,
        string persistenceGooType,
        string persistenceProperty,
        string persistenceValue)
    {
        Product = product;
        PluginPath = pluginPath;
        PluginAssemblyName = pluginAssemblyName;
        TypesAssemblyName = typesAssemblyName;
        ParameterNamespace = parameterNamespace;
        PersistenceParameterType = persistenceParameterType;
        PersistenceGooType = persistenceGooType;
        PersistenceProperty = persistenceProperty;
        PersistenceValue = persistenceValue;
    }

    internal string Product { get; }

    internal string PluginPath { get; }

    internal string PluginAssemblyName { get; }

    internal string TypesAssemblyName { get; }

    internal string ParameterNamespace { get; }

    internal string PersistenceParameterType { get; }

    internal string PersistenceGooType { get; }

    internal string PersistenceProperty { get; }

    internal string PersistenceValue { get; }

    internal string TypesPath => Path.Combine(
        Path.GetDirectoryName(PluginPath)!,
        TypesAssemblyName + ".dll");

    internal static IReadOnlyList<DragonModuleSpec> FromInputs(SmokeHostInputs inputs)
    {
        return inputs.PluginPaths.Select(FromPluginPath).ToArray();
    }

    internal static DragonModuleSpec FromPluginPath(string pluginPath)
    {
        string fileName = Path.GetFileName(pluginPath);
        if (string.Equals(fileName, InvisibleGhaFileName, StringComparison.OrdinalIgnoreCase))
        {
            return new DragonModuleSpec(
                "InvisibleDragon",
                pluginPath,
                "GonieGonie.InvisibleDragon.GH",
                "GonieGonie.InvisibleDragon.Grasshopper.Types",
                "GonieGonie.InvisibleDragon.Grasshopper.Parameters",
                "GonieGonie.InvisibleDragon.Grasshopper.Parameters.DiagnosticParam",
                "GonieGonie.InvisibleDragon.Grasshopper.Types.DiagnosticGoo",
                "Code",
                "DRAGON_HOST_GATE");
        }

        if (string.Equals(fileName, SimpleGhaFileName, StringComparison.OrdinalIgnoreCase))
        {
            return new DragonModuleSpec(
                "SimpleDragon",
                pluginPath,
                "GonieGonie.SimpleDragon.GH",
                "GonieGonie.SimpleDragon.Grasshopper.Types",
                "GonieGonie.SimpleDragon.Grasshopper.Parameters",
                "GonieGonie.SimpleDragon.Grasshopper.Parameters.SimpleDragonMaterialParam",
                "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonMaterialGoo",
                "Name",
                "Smoke Simple Material");
        }

        throw new InvalidOperationException("Unsupported Dragon GHA: " + pluginPath);
    }
}
