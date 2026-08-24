using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GonieGonie.PackageVerifier;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Dictionary<string, string> options = ParseArguments(args);
            string packagesRoot = RequiredOption(options, "--packages-root");
            string specPath = RequiredOption(options, "--spec");
            options.TryGetValue("--report", out string? reportPath);

            PackageSpec spec = JsonSerializer.Deserialize<PackageSpec>(
                File.ReadAllText(specPath),
                JsonOptions()) ?? throw new InvalidDataException("Package specification is empty.");
            var verifier = new PackageVerifier(Path.GetFullPath(packagesRoot), spec);
            VerificationReport report = verifier.Verify();
            string json = JsonSerializer.Serialize(report, JsonOptions(writeIndented: true)) + Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                string fullReportPath = Path.GetFullPath(reportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
                File.WriteAllText(fullReportPath, json, new UTF8Encoding(false));
            }

            Console.WriteLine(json.TrimEnd());
            return report.Success ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.GetType().Name + ": " + exception.Message);
            return 2;
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Arguments must use '--name value' pairs.");
            }

            result.Add(args[index], args[index + 1]);
        }

        return result;
    }

    private static string RequiredOption(Dictionary<string, string> options, string name)
    {
        return options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("Missing required option '" + name + "'.");
    }

    private static JsonSerializerOptions JsonOptions(bool writeIndented = false) => new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = writeIndented,
    };
}

internal sealed class PackageVerifier
{
    private const string InvisibleScenario = "InvisibleDragon-only";
    private const string SimpleScenario = "SimpleDragon-only";
    private const string BothScenario = "both";
    private static readonly string[] RequiredRootFiles =
    {
        "manifest.yml",
        "icon.png",
        "README.md",
        "LICENSE.txt",
        "NOTICE.md",
        "package-manifest.json",
        "checksums.sha256",
    };
    private static readonly string[] NewLineSeparators = { "\r\n", "\n" };

    private readonly string _packagesRoot;
    private readonly PackageSpec _spec;
    private readonly List<VerificationFailure> _failures = new();
    private readonly SortedDictionary<string, SortedDictionary<string, string>> _sharedHashes =
        new(StringComparer.Ordinal);

    public PackageVerifier(string packagesRoot, PackageSpec spec)
    {
        _packagesRoot = packagesRoot;
        _spec = spec;
    }

    public VerificationReport Verify()
    {
        Check(BothScenario, _spec.Schema == "goniegonie.dragons-grasshopper.package-spec.v1",
            "Unsupported package specification schema '" + _spec.Schema + "'.");
        Check(BothScenario, Regex.IsMatch(_spec.Version, @"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$"),
            "Package version is not SemVer: '" + _spec.Version + "'.");
        Check(BothScenario, Directory.Exists(_packagesRoot), "Packages root does not exist: '" + _packagesRoot + "'.");

        foreach (ProductSpec product in _spec.Products)
        {
            VerifyProduct(product);
        }

        VerifySharedCompatibility();
        string rootChecksums = Path.Combine(_packagesRoot, "checksums.sha256");
        if (File.Exists(rootChecksums))
        {
            VerifyChecksums(BothScenario, _packagesRoot);
        }

        bool invisibleSuccess = !_failures.Any(item => item.Scenario is InvisibleScenario or BothScenario);
        bool simpleSuccess = !_failures.Any(item => item.Scenario is SimpleScenario or BothScenario);
        bool bothSuccess = _failures.Count == 0;
        return new VerificationReport
        {
            Schema = "goniegonie.dragons-grasshopper.package-verification.v1",
            Version = _spec.Version,
            Success = bothSuccess,
            Scenarios = new SortedDictionary<string, bool>(StringComparer.Ordinal)
            {
                [InvisibleScenario] = invisibleSuccess,
                [SimpleScenario] = simpleSuccess,
                [BothScenario] = bothSuccess,
            },
            SharedAssemblies = _sharedHashes,
            Failures = _failures,
        };
    }

    private void VerifyProduct(ProductSpec product)
    {
        string scenario = product.Id == "invisible-dragon" ? InvisibleScenario : SimpleScenario;
        string productRoot = Path.Combine(_packagesRoot, product.Id);
        Check(scenario, Directory.Exists(productRoot), "Product output is missing: '" + productRoot + "'.");
        if (!Directory.Exists(productRoot))
        {
            return;
        }

        foreach (TargetSpec target in _spec.Targets)
        {
            VerifyStage(scenario, product, target, Path.Combine(productRoot, "stage", target.Id));
        }

        VerifyYakArchives(scenario, product, Path.Combine(productRoot, "yak"));
        VerifyPortableArchive(scenario, product, Path.Combine(productRoot, "portable"));
    }

    private void VerifyStage(string scenario, ProductSpec product, TargetSpec target, string stageRoot)
    {
        Check(scenario, Directory.Exists(stageRoot), "Yak stage is missing: '" + stageRoot + "'.");
        if (!Directory.Exists(stageRoot))
        {
            return;
        }

        VerifyRootMetadata(scenario, product, stageRoot, "yak-stage");
        VerifyForbiddenPaths(scenario, Directory.EnumerateFiles(stageRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(stageRoot, path)));
        VerifyChecksums(scenario, stageRoot);

        if (target.YakLayout == "flat")
        {
            Check(scenario, target.Frameworks.Count == 1 && target.Frameworks[0] == "net48",
                "Rhino 7 must have exactly one net48 framework in the package spec.");
            Check(scenario, File.Exists(Path.Combine(stageRoot, product.EntryAssembly)),
                "Rhino 7 entry GHA must be at the Yak root for " + product.DisplayName + ".");
            Check(scenario, !Directory.EnumerateDirectories(stageRoot).Any(),
                "Rhino 7 Yak stage must be flat and cannot contain framework directories.");
            VerifyPayloadDirectory(scenario, product, "rhino7/net48", stageRoot);
        }
        else
        {
            string[] actualDirectories = Directory.EnumerateDirectories(stageRoot)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()!;
            string[] expectedDirectories = target.Frameworks.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Check(scenario, actualDirectories.SequenceEqual(expectedDirectories, StringComparer.Ordinal),
                "Rhino 8 stage framework directories differ. Expected "
                + string.Join(", ", expectedDirectories) + "; found " + string.Join(", ", actualDirectories) + ".");
            foreach (string framework in target.Frameworks)
            {
                string payloadRoot = Path.Combine(stageRoot, framework);
                Check(scenario, File.Exists(Path.Combine(payloadRoot, product.EntryAssembly)),
                    product.DisplayName + " entry GHA is missing from Rhino 8/" + framework + ".");
                VerifyPayloadDirectory(scenario, product, "rhino8/" + framework, payloadRoot);
            }
        }
    }

    private void VerifyRootMetadata(
        string scenario,
        ProductSpec product,
        string root,
        string expectedKind)
    {
        foreach (string name in RequiredRootFiles)
        {
            Check(scenario, File.Exists(Path.Combine(root, name)),
                "Required package-root file '" + name + "' is missing from '" + root + "'.");
        }

        string manifestPath = Path.Combine(root, "manifest.yml");
        if (File.Exists(manifestPath))
        {
            VerifyManifest(scenario, product, File.ReadAllText(manifestPath), root);
        }

        string payloadManifestPath = Path.Combine(root, "package-manifest.json");
        if (File.Exists(payloadManifestPath))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(payloadManifestPath));
            JsonElement package = document.RootElement;
            Check(scenario, package.GetProperty("schema").GetString()
                    == "goniegonie.dragons-grasshopper.payload-manifest.v1",
                "Payload manifest schema mismatch in '" + payloadManifestPath + "'.");
            Check(scenario, package.GetProperty("product").GetProperty("id").GetString() == product.Id,
                "Payload manifest product mismatch in '" + payloadManifestPath + "'.");
            Check(scenario, package.GetProperty("product").GetProperty("version").GetString() == _spec.Version,
                "Payload manifest version mismatch in '" + payloadManifestPath + "'.");
            Check(scenario, package.GetProperty("kind").GetString() == expectedKind,
                "Payload manifest kind mismatch in '" + payloadManifestPath + "'.");
            JsonElement runtime = package.GetProperty("runtime");
            Check(scenario, !runtime.GetProperty("energyPlusBinariesIncluded").GetBoolean()
                    && !runtime.GetProperty("weatherIncluded").GetBoolean()
                    && !runtime.GetProperty("pythonRequired").GetBoolean(),
                "Payload manifest must declare a Python-free plugin payload without EnergyPlus/weather redistribution.");
        }
    }

    private void VerifyManifest(string scenario, ProductSpec product, string text, string root)
    {
        string name = ManifestScalar(text, "name");
        string version = ManifestScalar(text, "version");
        string icon = ManifestScalar(text, "icon");
        Check(scenario, name == product.Id, "Yak manifest name mismatch for " + product.DisplayName + ".");
        Check(scenario, version == _spec.Version, "Yak manifest version mismatch for " + product.DisplayName + ".");
        Check(scenario, icon == "icon.png", "Yak manifest icon must be 'icon.png' for " + product.DisplayName + ".");
        Check(scenario, File.Exists(Path.Combine(root, icon)), "Yak manifest icon is missing for " + product.DisplayName + ".");
        Check(scenario, text.Contains("Gonie-Gonie", StringComparison.Ordinal),
            "Yak manifest must identify Gonie-Gonie as author for " + product.DisplayName + ".");
    }

    private void VerifyPayloadDirectory(string scenario, ProductSpec product, string targetKey, string payloadRoot)
    {
        if (!Directory.Exists(payloadRoot))
        {
            Failure(scenario, "Payload directory is missing: '" + payloadRoot + "'.");
            return;
        }

        foreach (string required in product.RequiredAssemblies)
        {
            Check(scenario, File.Exists(Path.Combine(payloadRoot, required)),
                product.DisplayName + " required assembly '" + required + "' is missing from " + targetKey + ".");
        }

        string[] files = Directory.EnumerateFiles(payloadRoot, "*", SearchOption.TopDirectoryOnly).ToArray();
        Check(scenario, !files.Any(path => Path.GetFileName(path).StartsWith("RhinoCommon", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).StartsWith("Grasshopper", StringComparison.OrdinalIgnoreCase)),
            "Host-owned RhinoCommon/Grasshopper assemblies are present in " + product.DisplayName + " " + targetKey + ".");
        Check(scenario, !files.Any(path => Path.GetExtension(path) is ".pdb" or ".xml"),
            "Debug symbols or XML documentation are present in " + product.DisplayName + " " + targetKey + ".");
        if (product.Id == "invisible-dragon")
        {
            Check(scenario, !files.Any(path => Path.GetFileName(path).StartsWith("GonieGonie.SimpleDragon.", StringComparison.Ordinal)),
                "InvisibleDragon-only payload contains a SimpleDragon assembly in " + targetKey + ".");
        }
        else
        {
            Check(scenario, !File.Exists(Path.Combine(payloadRoot, "GonieGonie.InvisibleDragon.GH.gha")),
                "SimpleDragon-only payload must not contain the InvisibleDragon component GHA in " + targetKey + ".");
        }

        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in files.Where(IsManagedPayloadFile))
        {
            AssemblyMetadata metadata = ReadAssembly(path);
            if (!identities.TryAdd(metadata.Name, path))
            {
                Failure(scenario, "Duplicate assembly identity '" + metadata.Name + "' in " + product.DisplayName
                    + " " + targetKey + ": '" + identities[metadata.Name] + "' and '" + path + "'.");
            }

            if (metadata.Name.StartsWith("GonieGonie.", StringComparison.Ordinal))
            {
                Check(scenario, metadata.Version == new Version(0, 1, 0, 0),
                    "Assembly version mismatch for '" + path + "': " + metadata.Version + ".");
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
                Check(scenario, version.FileVersion == "0.1.0.0",
                    "File version mismatch for '" + path + "': '" + version.FileVersion + "'.");
                Check(scenario, version.ProductVersion is not null
                        && version.ProductVersion.StartsWith(_spec.Version, StringComparison.Ordinal),
                    "Informational/product version mismatch for '" + path + "': '" + version.ProductVersion + "'.");
            }
        }

        string entryPath = Path.Combine(payloadRoot, product.EntryAssembly);
        if (File.Exists(entryPath))
        {
            int expectedRhinoMajor = targetKey.StartsWith("rhino7/", StringComparison.Ordinal) ? 7 : 8;
            AssemblyReferenceIdentity? rhinoReference = ReadAssembly(entryPath).References
                .SingleOrDefault(reference => reference.Name == "RhinoCommon");
            Check(scenario, rhinoReference is not null && rhinoReference.Version.Major == expectedRhinoMajor,
                product.DisplayName + " entry GHA references RhinoCommon "
                + (rhinoReference?.Version.ToString() ?? "<missing>") + " in " + targetKey
                + "; expected Rhino " + expectedRhinoMajor + ".");
        }

        if (product.Id == "simple-dragon")
        {
            bool hasInteropReference = files.Where(IsManagedPayloadFile)
                .Select(ReadAssembly)
                .SelectMany(item => item.References)
                .Any(reference => reference.Name == "GonieGonie.InvisibleDragon.Grasshopper.Types"
                    && reference.Version == new Version(0, 1, 0, 0));
            Check(scenario, hasInteropReference,
                "SimpleDragon " + targetKey + " does not reference the shared InvisibleDragon Grasshopper types at 0.1.0.0.");
        }
    }

    private void VerifyYakArchives(string scenario, ProductSpec product, string yakRoot)
    {
        Check(scenario, Directory.Exists(yakRoot), "Yak output directory is missing for " + product.DisplayName + ".");
        if (!Directory.Exists(yakRoot))
        {
            return;
        }

        string[] expected =
        {
            product.Id + "-" + _spec.Version + "-rh7-win.yak",
            product.Id + "-" + _spec.Version + "-rh8-win.yak",
        };
        string[] actual = Directory.EnumerateFiles(yakRoot, "*.yak")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;
        Check(scenario, actual.SequenceEqual(expected.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal),
            "Yak filenames/distribution tags differ for " + product.DisplayName + ". Expected "
            + string.Join(", ", expected) + "; found " + string.Join(", ", actual) + ".");

        foreach (string fileName in expected)
        {
            string path = Path.Combine(yakRoot, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            bool rhino7 = fileName.EndsWith("-rh7-win.yak", StringComparison.Ordinal);
            using ZipArchive archive = ZipFile.OpenRead(path);
            VerifyArchiveRoot(scenario, product, archive, rhino7 ? "rhino7" : "rhino8");
        }
    }

    private void VerifyArchiveRoot(string scenario, ProductSpec product, ZipArchive archive, string target)
    {
        string[] entries = archive.Entries.Where(item => !string.IsNullOrEmpty(item.Name))
            .Select(item => NormalizeArchivePath(item.FullName)).ToArray();
        VerifyForbiddenPaths(scenario, entries);
        foreach (string name in RequiredRootFiles)
        {
            Check(scenario, entries.Contains(name, StringComparer.Ordinal),
                "Yak archive is missing root file '" + name + "' for " + product.DisplayName + " " + target + ".");
        }

        ZipArchiveEntry? manifest = FindEntry(archive, "manifest.yml");
        if (manifest is not null)
        {
            using var reader = new StreamReader(manifest.Open(), Encoding.UTF8, true, 1024, leaveOpen: false);
            string text = reader.ReadToEnd();
            Check(scenario, ManifestScalar(text, "name") == product.Id
                    && ManifestScalar(text, "version") == _spec.Version
                    && ManifestScalar(text, "icon") == "icon.png",
                "Yak archive manifest identity/version/icon mismatch for " + product.DisplayName + " " + target + ".");
        }

        if (target == "rhino7")
        {
            Check(scenario, entries.Contains(product.EntryAssembly, StringComparer.Ordinal),
                "Rhino 7 Yak archive entry GHA is not at the root for " + product.DisplayName + ".");
            Check(scenario, !entries.Any(item => item.StartsWith("net48/", StringComparison.Ordinal)),
                "Rhino 7 Yak archive incorrectly nests the net48 payload.");
        }
        else
        {
            Check(scenario, entries.Contains("net7.0/" + product.EntryAssembly, StringComparer.Ordinal)
                    && entries.Contains("net8.0/" + product.EntryAssembly, StringComparer.Ordinal),
                "Rhino 8 Yak archive does not contain both net7.0 and net8.0 entry GHAs for " + product.DisplayName + ".");
        }
    }

    private void VerifyPortableArchive(string scenario, ProductSpec product, string portableRoot)
    {
        string expectedName = product.Id + "-" + _spec.Version + "-portable-plugin-win.zip";
        string path = Path.Combine(portableRoot, expectedName);
        Check(scenario, File.Exists(path), "Portable plugin ZIP is missing: '" + path + "'.");
        if (!File.Exists(path))
        {
            return;
        }

        string[] actual = Directory.EnumerateFiles(portableRoot, "*.zip").Select(Path.GetFileName).ToArray()!;
        Check(scenario, actual.Length == 1 && actual[0] == expectedName,
            "Portable output must contain exactly the versioned plugin-only ZIP for " + product.DisplayName + ".");
        using ZipArchive archive = ZipFile.OpenRead(path);
        string[] entries = archive.Entries.Where(item => !string.IsNullOrEmpty(item.Name))
            .Select(item => NormalizeArchivePath(item.FullName)).ToArray();
        VerifyForbiddenPaths(scenario, entries);
        foreach (string name in RequiredRootFiles)
        {
            Check(scenario, entries.Contains(name, StringComparer.Ordinal),
                "Portable ZIP is missing root file '" + name + "' for " + product.DisplayName + ".");
        }
        foreach (TargetSpec target in _spec.Targets)
        {
            foreach (string framework in target.Frameworks)
            {
                string prefix = target.Id + "/" + framework + "/";
                Check(scenario, entries.Contains(prefix + product.EntryAssembly, StringComparer.Ordinal),
                    "Portable ZIP is missing " + prefix + product.EntryAssembly + ".");
                VerifyArchiveAssemblyDuplicates(scenario, product, archive, prefix);
            }
        }
        VerifyArchiveChecksums(scenario, archive);
    }

    private void VerifyArchiveAssemblyDuplicates(
        string scenario,
        ProductSpec product,
        ZipArchive archive,
        string prefix)
    {
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ZipArchiveEntry entry in archive.Entries.Where(item =>
            NormalizeArchivePath(item.FullName).StartsWith(prefix, StringComparison.Ordinal)
            && IsManagedPayloadFile(item.Name)))
        {
            AssemblyMetadata metadata = ReadAssembly(entry);
            string path = NormalizeArchivePath(entry.FullName);
            if (!identities.TryAdd(metadata.Name, path))
            {
                Failure(scenario, "Portable ZIP contains duplicate assembly identity '" + metadata.Name
                    + "' for " + product.DisplayName + " under " + prefix + ".");
            }
        }
    }

    private void VerifySharedCompatibility()
    {
        ProductSpec? invisible = _spec.Products.SingleOrDefault(item => item.Id == "invisible-dragon");
        ProductSpec? simple = _spec.Products.SingleOrDefault(item => item.Id == "simple-dragon");
        if (invisible is null || simple is null)
        {
            Failure(BothScenario, "Package spec must define both InvisibleDragon and SimpleDragon products.");
            return;
        }

        foreach (TargetSpec target in _spec.Targets)
        {
            foreach (string framework in target.Frameworks)
            {
                string invisibleRoot = PayloadRoot(invisible, target, framework);
                string simpleRoot = PayloadRoot(simple, target, framework);
                string targetKey = target.Id + "/" + framework;
                var targetHashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (string shared in _spec.SharedAssemblies)
                {
                    string first = Path.Combine(invisibleRoot, shared);
                    string second = Path.Combine(simpleRoot, shared);
                    if (!File.Exists(first) || !File.Exists(second))
                    {
                        Failure(BothScenario, "Shared assembly '" + shared + "' is missing from one or both " + targetKey + " payloads.");
                        continue;
                    }

                    string firstHash = Sha256(first);
                    string secondHash = Sha256(second);
                    Check(BothScenario, firstHash == secondHash,
                        "Shared assembly SHA-256 mismatch for '" + shared + "' in " + targetKey + ".");
                    targetHashes[shared] = firstHash;
                }
                _sharedHashes[targetKey] = targetHashes;

                string sharedTypes = "GonieGonie.InvisibleDragon.Grasshopper.Types.dll";
                string firstTypes = Path.Combine(invisibleRoot, sharedTypes);
                string secondTypes = Path.Combine(simpleRoot, sharedTypes);
                if (File.Exists(firstTypes) && File.Exists(secondTypes))
                {
                    string[] firstPublic = ReadAssembly(firstTypes).PublicTypes;
                    string[] secondPublic = ReadAssembly(secondTypes).PublicTypes;
                    Check(BothScenario, firstPublic.SequenceEqual(secondPublic, StringComparer.Ordinal),
                        "Shared Grasshopper type surface differs between payloads in " + targetKey + ".");
                    foreach (string requiredType in _spec.InteropTypes)
                    {
                        Check(BothScenario, firstPublic.Contains(requiredType, StringComparer.Ordinal),
                            "Required interop type '" + requiredType + "' is absent in " + targetKey + ".");
                    }
                }

                var union = new Dictionary<string, (string Hash, string Path)>(StringComparer.Ordinal);
                foreach (string path in Directory.EnumerateFiles(invisibleRoot).Concat(Directory.EnumerateFiles(simpleRoot))
                    .Where(IsManagedPayloadFile))
                {
                    AssemblyMetadata metadata = ReadAssembly(path);
                    string hash = Sha256(path);
                    if (union.TryGetValue(metadata.Name, out (string Hash, string Path) known))
                    {
                        Check(BothScenario, known.Hash == hash,
                            "Both-installed payload has conflicting copies of assembly identity '" + metadata.Name
                            + "' in " + targetKey + ": '" + known.Path + "' and '" + path + "'.");
                    }
                    else
                    {
                        union.Add(metadata.Name, (hash, path));
                    }
                }
            }
        }
    }

    private string PayloadRoot(ProductSpec product, TargetSpec target, string framework)
    {
        string stage = Path.Combine(_packagesRoot, product.Id, "stage", target.Id);
        return target.YakLayout == "flat" ? stage : Path.Combine(stage, framework);
    }

    private void VerifyForbiddenPaths(string scenario, IEnumerable<string> paths)
    {
        foreach (string rawPath in paths)
        {
            string path = rawPath.Replace('\\', '/');
            string lower = path.ToLowerInvariant();
            string name = Path.GetFileName(lower);
            bool forbidden = lower.Split('/').Any(part => part is "__pycache__" or "python" or "weather")
                || name.EndsWith(".py", StringComparison.Ordinal)
                || name.EndsWith(".pyc", StringComparison.Ordinal)
                || name.EndsWith(".pyd", StringComparison.Ordinal)
                || name.StartsWith("python", StringComparison.Ordinal)
                || name is "rhinocommon.dll" or "grasshopper.dll" or "gh_io.dll"
                    or "rhino.ui.dll" or "eto.dll" or "ed.eto.dll"
                    or "goniegonie.yakinspectionhost.dll" or "yak.exe"
                    or "energyplus.exe" or "expandobjects.exe"
                || name.EndsWith(".epw", StringComparison.Ordinal)
                || name.EndsWith(".idd", StringComparison.Ordinal)
                || name.EndsWith(".pdb", StringComparison.Ordinal)
                || name.EndsWith(".xml", StringComparison.Ordinal);
            Check(scenario, !forbidden, "Forbidden redistributed payload path: '" + path + "'.");
        }
    }

    private void VerifyChecksums(string scenario, string root)
    {
        string checksumPath = Path.Combine(root, "checksums.sha256");
        if (!File.Exists(checksumPath))
        {
            Failure(scenario, "Checksum file is missing: '" + checksumPath + "'.");
            return;
        }

        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(checksumPath))
        {
            Match match = Regex.Match(line, "^(?<hash>[0-9a-f]{64})  (?<path>.+)$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                Failure(scenario, "Invalid SHA-256 line in '" + checksumPath + "': '" + line + "'.");
                continue;
            }

            string relative = match.Groups["path"].Value;
            listed.Add(relative);
            string fullPath = SafeRelativePath(root, relative);
            Check(scenario, File.Exists(fullPath), "Checksum target is missing: '" + fullPath + "'.");
            if (File.Exists(fullPath))
            {
                Check(scenario, Sha256(fullPath) == match.Groups["hash"].Value,
                    "SHA-256 mismatch for '" + fullPath + "'.");
            }
        }

        string[] expected = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Equals(checksumPath, StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Check(scenario, listed.OrderBy(path => path, StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal),
            "Checksum inventory is incomplete or contains extra paths in '" + root + "'.");
    }

    private void VerifyArchiveChecksums(string scenario, ZipArchive archive)
    {
        ZipArchiveEntry? checksumEntry = FindEntry(archive, "checksums.sha256");
        if (checksumEntry is null)
        {
            Failure(scenario, "Archive checksum file is missing.");
            return;
        }

        string checksumText;
        using (var reader = new StreamReader(checksumEntry.Open(), Encoding.UTF8, true, 1024, leaveOpen: false))
        {
            checksumText = reader.ReadToEnd();
        }
        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in checksumText.Split(NewLineSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = Regex.Match(line, "^(?<hash>[0-9a-f]{64})  (?<path>.+)$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                Failure(scenario, "Invalid checksum line inside portable ZIP: '" + line + "'.");
                continue;
            }
            string path = match.Groups["path"].Value;
            listed.Add(path);
            ZipArchiveEntry? entry = FindEntry(archive, path);
            Check(scenario, entry is not null, "Portable ZIP checksum target is missing: '" + path + "'.");
            if (entry is not null)
            {
                using Stream stream = entry.Open();
                Check(scenario, Sha256(stream) == match.Groups["hash"].Value,
                    "Portable ZIP SHA-256 mismatch for '" + path + "'.");
            }
        }

        string[] expected = archive.Entries.Where(item => !string.IsNullOrEmpty(item.Name))
            .Select(item => NormalizeArchivePath(item.FullName))
            .Where(path => path != "checksums.sha256")
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Check(scenario, listed.OrderBy(path => path, StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal),
            "Portable ZIP checksum inventory is incomplete or contains extra paths.");
    }

    private static AssemblyMetadata ReadAssembly(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return ReadAssembly(stream, path);
    }

    private static AssemblyMetadata ReadAssembly(ZipArchiveEntry entry)
    {
        using Stream source = entry.Open();
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        buffer.Position = 0;
        return ReadAssembly(buffer, entry.FullName);
    }

    private static AssemblyMetadata ReadAssembly(Stream stream, string description)
    {
        using var pe = new PEReader(stream, PEStreamOptions.PrefetchMetadata);
        if (!pe.HasMetadata)
        {
            throw new InvalidDataException("Managed payload file has no CLR metadata: '" + description + "'.");
        }

        MetadataReader reader = pe.GetMetadataReader();
        AssemblyDefinition definition = reader.GetAssemblyDefinition();
        string name = reader.GetString(definition.Name);
        AssemblyReferenceIdentity[] references = reader.AssemblyReferences
            .Select(handle => reader.GetAssemblyReference(handle))
            .Select(reference => new AssemblyReferenceIdentity(
                reader.GetString(reference.Name),
                reference.Version))
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
        string[] publicTypes = reader.TypeDefinitions
            .Select(handle => reader.GetTypeDefinition(handle))
            .Where(type => (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public)
            .Select(type =>
            {
                string namespaceName = reader.GetString(type.Namespace);
                string typeName = reader.GetString(type.Name);
                return string.IsNullOrEmpty(namespaceName) ? typeName : namespaceName + "." + typeName;
            })
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        return new AssemblyMetadata(name, definition.Version, references, publicTypes);
    }

    private static bool IsManagedPayloadFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gha", StringComparison.OrdinalIgnoreCase);
    }

    private static string ManifestScalar(string yaml, string name)
    {
        Match match = Regex.Match(yaml, "^" + Regex.Escape(name) + @":\s*(?<value>[^\r\n#]+?)\s*$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim().Trim('"', '\'') : string.Empty;
    }

    private static string SafeRelativePath(string root, string relative)
    {
        string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string path = Path.GetFullPath(Path.Combine(normalizedRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = normalizedRoot + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Checksum path escapes its package root: '" + relative + "'.");
        }
        return path;
    }

    private static string NormalizeArchivePath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string path)
    {
        string normalized = NormalizeArchivePath(path);
        return archive.Entries.FirstOrDefault(item =>
            NormalizeArchivePath(item.FullName).Equals(normalized, StringComparison.Ordinal));
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Sha256(stream);
    }

    private static string Sha256(Stream stream)
    {
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void Check(string scenario, bool condition, string message)
    {
        if (!condition)
        {
            Failure(scenario, message);
        }
    }

    private void Failure(string scenario, string message)
    {
        _failures.Add(new VerificationFailure { Scenario = scenario, Message = message });
    }
}

internal sealed class PackageSpec
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("targets")]
    public List<TargetSpec> Targets { get; set; } = new();

    [JsonPropertyName("shared_assemblies")]
    public List<string> SharedAssemblies { get; set; } = new();

    [JsonPropertyName("interop_types")]
    public List<string> InteropTypes { get; set; } = new();

    [JsonPropertyName("products")]
    public List<ProductSpec> Products { get; set; } = new();
}

internal sealed class TargetSpec
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("frameworks")]
    public List<string> Frameworks { get; set; } = new();

    [JsonPropertyName("yak_layout")]
    public string YakLayout { get; set; } = string.Empty;

    [JsonPropertyName("distribution_tag")]
    public string DistributionTag { get; set; } = string.Empty;
}

internal sealed class ProductSpec
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("entry_assembly")]
    public string EntryAssembly { get; set; } = string.Empty;

    [JsonPropertyName("required_assemblies")]
    public List<string> RequiredAssemblies { get; set; } = new();
}

internal sealed class VerificationReport
{
    public string Schema { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public bool Success { get; set; }

    public SortedDictionary<string, bool> Scenarios { get; set; } = new(StringComparer.Ordinal);

    public SortedDictionary<string, SortedDictionary<string, string>> SharedAssemblies { get; set; } =
        new(StringComparer.Ordinal);

    public IReadOnlyList<VerificationFailure> Failures { get; set; } = Array.Empty<VerificationFailure>();
}

internal sealed class VerificationFailure
{
    public string Scenario { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

internal sealed record AssemblyMetadata(
    string Name,
    Version Version,
    AssemblyReferenceIdentity[] References,
    string[] PublicTypes);

internal sealed record AssemblyReferenceIdentity(string Name, Version Version);
