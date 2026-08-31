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
            string stageRoot = RequiredOption(options, "--stage-root");
            string specPath = RequiredOption(options, "--spec");
            string distributionsPath = RequiredOption(options, "--distributions");
            string repositoryRoot = Path.GetFullPath(RequiredOption(options, "--repository-root"));
            options.TryGetValue("--report", out string? reportPath);

            PackageSpec spec = JsonSerializer.Deserialize<PackageSpec>(
                File.ReadAllText(specPath),
                JsonOptions()) ?? throw new InvalidDataException("Package specification is empty.");
            DistributionManifest distributions = JsonSerializer.Deserialize<DistributionManifest>(
                File.ReadAllText(distributionsPath),
                JsonOptions()) ?? throw new InvalidDataException("Distribution manifest is empty.");
            var verifier = new PackageVerifier(
                Path.GetFullPath(packagesRoot),
                Path.GetFullPath(stageRoot),
                repositoryRoot,
                spec,
                distributions);
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
    private static readonly string[] EmbeddedRuntimeRootDirectory = { "runtime" };

    private readonly string _packagesRoot;
    private readonly string _stageRoot;
    private readonly string _repositoryRoot;
    private readonly PackageSpec _spec;
    private readonly DistributionManifest _distributions;
    private readonly List<VerificationFailure> _failures = new();
    private readonly SortedDictionary<string, SortedDictionary<string, string>> _sharedHashes =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _deepValidatedDistributionHashes = new(StringComparer.Ordinal);

    public PackageVerifier(
        string packagesRoot,
        string stageRoot,
        string repositoryRoot,
        PackageSpec spec,
        DistributionManifest distributions)
    {
        _packagesRoot = packagesRoot;
        _stageRoot = stageRoot;
        _repositoryRoot = repositoryRoot;
        _spec = spec;
        _distributions = distributions;
    }

    public VerificationReport Verify()
    {
        Check(BothScenario, _spec.Schema == "goniegonie.dragons-grasshopper.package-spec.v2",
            "Unsupported package specification schema '" + _spec.Schema + "'.");
        Check(BothScenario, Regex.IsMatch(_spec.Version, @"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$"),
            "Package version is not SemVer: '" + _spec.Version + "'.");
        VerifyPublicationSpec();
        Check(BothScenario, Directory.Exists(_packagesRoot), "Packages root does not exist: '" + _packagesRoot + "'.");
        VerifyDistributionManifest();

        foreach (ProductSpec product in _spec.Products)
        {
            VerifyProduct(product);
        }

        VerifySharedCompatibility();
        VerifyPackageIndex();
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

    private void VerifyDistributionManifest()
    {
        Check(BothScenario, _distributions.Schema == "goniegonie.dragons-grasshopper.distributions.v2",
            "Unsupported distribution manifest schema '" + _distributions.Schema + "'.");
        Check(BothScenario, _distributions.Payloads.Count == 2,
            "Distribution manifest must contain exactly two reviewed payloads.");

        VerifyDistributionPin(
            "invisible-dragon",
            "energyplus-24.2.0-windows-x64",
            "energyplus-archive",
            "EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip",
            "https://github.com/NREL/EnergyPlus/releases/download/v24.2.0a/EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip",
            179248139,
            "26c7c22b731f54031626750284c8b613fb8f03c3aa56b6bc7ec65b6bf8668df1",
            "energyplus/EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip",
            "runtime/energyplus/EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip");
        VerifyDistributionPin(
            "simple-dragon",
            "korean-tmy-v1",
            "weather-archive",
            "KoreanTMY-v1.zip",
            "https://github.com/snu-bslab/EPlusSimple-resources/releases/download/weather/v1/KoreanTMY-v1.zip",
            128349513,
            "fa88b8d69364b6a6b663afdc6dc2eb30c0ddee17cd37e5802ce5a5dec63d92d0",
            "weather/KoreanTMY-v1.zip",
            "runtime/weather/KoreanTMY-v1.zip");

        DistributionPayload? weather = DistributionFor("simple-dragon");
        if (weather is not null)
        {
            Check(SimpleScenario, weather.ArchiveEpwCount == 80
                    && weather.MetadataReferencedUniqueEpwCount == 78
                    && weather.MetadataPath == "data/simple-dragon/weather/행정구역별기상데이터.csv"
                    && weather.MetadataColumn == "EPW파일명",
                "KoreanTMY distribution coverage metadata must pin 80 root EPWs and all 78 metadata-referenced EPWs.");
            Check(SimpleScenario,
                weather.Origin.Site == _spec.Publication.WeatherSource
                && weather.Origin.Dataset == "TMYx"
                && weather.Origin.SourcePage == "https://climate.onebuilding.org/sources/default.html"
                && weather.Origin.SouthKoreaIndex == "https://climate.onebuilding.org/WMO_Region_2_Asia/KOR_South_Korea/index.html"
                && weather.Origin.Citation == "Lawrie, Linda K, Drury B Crawley. 2022. Development of Global Typical Meteorological Years (TMYx). https://climate.onebuilding.org"
                && weather.Origin.SolarDataSource == "ERA5"
                && weather.Origin.SolarDataProvider == "Oikolab"
                && weather.Origin.CopernicusLicense == "https://cds.climate.copernicus.eu/licences/licence-to-use-copernicus-products"
                && weather.Origin.OikolabTerms == "https://docs.oikolab.com/terms/"
                && weather.Origin.ReviewedAt == "2026-08-31"
                && weather.Origin.WeatherRedistributionStatus == _spec.Publication.WeatherRedistributionStatus,
                "KoreanTMY origin and redistribution review differ from package-spec.json.");
        }
        DistributionPayload? energyPlus = DistributionFor("invisible-dragon");
        if (energyPlus is not null)
        {
            Check(InvisibleScenario,
                energyPlus.LicenseEntry == "EnergyPlus-24.2.0-94a887817b-Windows-x86_64/LICENSE.txt"
                && energyPlus.PackageLicensePath == "runtime/energyplus/LICENSE.txt"
                && energyPlus.LicenseSize == 3182
                && energyPlus.LicenseSha256 == "b43f1553459a4bcc49d180b42123a64a54fcbb6213cd99ac6ac6aa32cb1c1a05",
                "EnergyPlus archive/package license identity differs from the reviewed contract.");
        }
    }

    private void VerifyPublicationSpec()
    {
        PublicationSpec publication = _spec.Publication;
        Check(BothScenario,
            publication.ProjectLicense == "MIT"
            && publication.ProjectLicenseOwner == "Gonie-Gonie"
            && publication.ProjectLicenseOwnerType == "individual"
            && publication.ProjectLicenseReview == "resolved-2026-08-31"
            && publication.PublicSupportEmail == "hyeonggon.jo@snu.ac.kr"
            && publication.PublicSupportEmailReview == "resolved-2026-08-31"
            && publication.WeatherSource == "https://climate.onebuilding.org/"
            && publication.WeatherRedistributionStatus == "blocked-permission-not-found",
            "Package specification publication metadata differs from the reviewed contract.");
    }

    private void VerifyDistributionPin(
        string product,
        string id,
        string kind,
        string fileName,
        string url,
        long size,
        string sha256,
        string developmentPath,
        string packagePath)
    {
        DistributionPayload[] matches = _distributions.Payloads.Where(item => item.Product == product).ToArray();
        string scenario = product == "invisible-dragon" ? InvisibleScenario : SimpleScenario;
        Check(scenario, matches.Length == 1, "Distribution manifest must define exactly one payload for " + product + ".");
        if (matches.Length != 1)
        {
            return;
        }

        DistributionPayload item = matches[0];
        Check(scenario, item.Id == id && item.Kind == kind && item.FileName == fileName
                && item.Url == url && item.Size == size && item.Sha256 == sha256
                && item.DevelopmentPath == developmentPath && item.PackagePath == packagePath,
            "Distribution identity/path pin differs from the reviewed contract for " + product + ".");
        Check(scenario, Uri.TryCreate(item.Url, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps,
            "Distribution URL must be HTTPS for " + product + ".");
    }

    private DistributionPayload? DistributionFor(string productId)
    {
        DistributionPayload[] matches = _distributions.Payloads.Where(item => item.Product == productId).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private void VerifyPackageIndex()
    {
        string path = Path.Combine(_packagesRoot, "package-index.json");
        Check(BothScenario, File.Exists(path), "Package index is missing: '" + path + "'.");
        if (!File.Exists(path))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        Check(BothScenario, root.GetProperty("schema").GetString()
                == "goniegonie.dragons-grasshopper.package-index.v2",
            "Package index schema mismatch.");
        JsonElement redistribution = root.GetProperty("redistribution");
        Check(BothScenario, redistribution.GetProperty("energyPlusBinariesIncluded").GetBoolean()
                && redistribution.GetProperty("weatherIncluded").GetBoolean()
                && !redistribution.GetProperty("portableArchivesArePluginOnly").GetBoolean()
                && !redistribution.GetProperty("publicPublicationAuthorized").GetBoolean()
                && redistribution.GetProperty("projectLicense").GetString() == _spec.Publication.ProjectLicense
                && redistribution.GetProperty("projectLicenseOwner").GetString() == _spec.Publication.ProjectLicenseOwner
                && redistribution.GetProperty("projectLicenseOwnerType").GetString() == _spec.Publication.ProjectLicenseOwnerType
                && redistribution.GetProperty("projectLicenseReview").GetString() == _spec.Publication.ProjectLicenseReview
                && redistribution.GetProperty("publicSupportEmail").GetString() == _spec.Publication.PublicSupportEmail
                && redistribution.GetProperty("publicSupportEmailReview").GetString() == _spec.Publication.PublicSupportEmailReview
                && redistribution.GetProperty("weatherSource").GetString() == _spec.Publication.WeatherSource
                && redistribution.GetProperty("weatherRedistributionStatus").GetString() == _spec.Publication.WeatherRedistributionStatus,
            "Package index redistribution/publication flags are not truthful for the embedded archives.");

        JsonElement[] products = root.GetProperty("products").EnumerateArray().ToArray();
        Check(BothScenario, products.Length == 2, "Package index must contain exactly two products.");
        foreach (ProductSpec product in _spec.Products)
        {
            JsonElement[] matches = products.Where(item => item.GetProperty("id").GetString() == product.Id).ToArray();
            string scenario = product.Id == "invisible-dragon" ? InvisibleScenario : SimpleScenario;
            Check(scenario, matches.Length == 1, "Package index must contain exactly one " + product.Id + " entry.");
            if (matches.Length == 1)
            {
                JsonElement runtime = matches[0].GetProperty("runtime");
                VerifyRuntimeFlags(scenario, product, runtime, "package index");
                VerifyEmbeddedPayloadElement(scenario, product, runtime.GetProperty("embeddedPayload"), "package index");
            }
        }
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
            VerifyStage(scenario, product, target, Path.Combine(_stageRoot, product.Id, target.Id));
        }

        VerifyYakArchives(
            scenario,
            product,
            Path.Combine(productRoot, "yak"),
            Path.Combine(_stageRoot, product.Id));
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
        VerifyForbiddenPaths(scenario, product, Directory.EnumerateFiles(stageRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(stageRoot, path)));
        VerifyEmbeddedPayloadFile(scenario, product, stageRoot);
        VerifyChecksums(scenario, stageRoot);

        if (target.YakLayout == "flat")
        {
            Check(scenario, target.Frameworks.Count == 1 && target.Frameworks[0] == "net48",
                "Rhino 7 must have exactly one net48 framework in the package spec.");
            Check(scenario, File.Exists(Path.Combine(stageRoot, product.EntryAssembly)),
                "Rhino 7 entry GHA must be at the Yak root for " + product.DisplayName + ".");
            string[] actualDirectories = Directory.EnumerateDirectories(stageRoot)
                .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray()!;
            Check(scenario, actualDirectories.SequenceEqual(EmbeddedRuntimeRootDirectory, StringComparer.Ordinal),
                "Rhino 7 Yak stage may contain only the product runtime archive directory.");
            VerifyPayloadDirectory(scenario, product, "rhino7/net48", stageRoot);
        }
        else
        {
            string[] actualDirectories = Directory.EnumerateDirectories(stageRoot)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()!;
            string[] expectedDirectories = target.Frameworks.Concat(EmbeddedRuntimeRootDirectory)
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
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
            VerifyRuntimeFlags(scenario, product, runtime, payloadManifestPath);
            JsonElement[] embedded = runtime.GetProperty("embeddedPayloads").EnumerateArray().ToArray();
            Check(scenario, embedded.Length == 1,
                "Payload manifest must declare exactly one product-specific embedded archive in '" + payloadManifestPath + "'.");
            if (embedded.Length == 1)
            {
                VerifyEmbeddedPayloadElement(scenario, product, embedded[0], payloadManifestPath);
            }
        }
    }

    private void VerifyRuntimeFlags(string scenario, ProductSpec product, JsonElement runtime, string description)
    {
        bool invisible = product.Id == "invisible-dragon";
        Check(scenario,
            runtime.GetProperty("energyPlusBinariesIncluded").GetBoolean() == invisible
            && runtime.GetProperty("weatherIncluded").GetBoolean() == !invisible
            && !runtime.GetProperty("pythonRequired").GetBoolean(),
            "Runtime flags are not product-exclusive/Python-free in " + description + ".");
    }

    private void VerifyEmbeddedPayloadElement(
        string scenario,
        ProductSpec product,
        JsonElement embedded,
        string description)
    {
        DistributionPayload? expected = DistributionFor(product.Id);
        if (expected is null)
        {
            Failure(scenario, "No unique distribution pin is available for " + product.Id + ".");
            return;
        }

        Check(scenario,
            embedded.GetProperty("id").GetString() == expected.Id
            && embedded.GetProperty("kind").GetString() == expected.Kind
            && embedded.GetProperty("path").GetString() == expected.PackagePath
            && embedded.GetProperty("fileName").GetString() == expected.FileName
            && embedded.GetProperty("size").GetInt64() == expected.Size
            && embedded.GetProperty("sha256").GetString() == expected.Sha256,
            "Embedded payload metadata differs from the reviewed distribution pin in " + description + ".");
        bool hasLicense = embedded.TryGetProperty("license", out JsonElement license);
        if (product.Id == "invisible-dragon")
        {
            Check(scenario, hasLicense
                    && license.GetProperty("archiveEntry").GetString() == expected.LicenseEntry
                    && license.GetProperty("path").GetString() == expected.PackageLicensePath
                    && license.GetProperty("size").GetInt64() == expected.LicenseSize
                    && license.GetProperty("sha256").GetString() == expected.LicenseSha256,
                "EnergyPlus license metadata differs from the exact nested archive entry in " + description + ".");
        }
        else
        {
            Check(scenario, !hasLicense, "SimpleDragon must not declare an EnergyPlus license payload in " + description + ".");
        }
    }

    private void VerifyEmbeddedPayloadFile(string scenario, ProductSpec product, string root)
    {
        DistributionPayload? expected = DistributionFor(product.Id);
        if (expected is null)
        {
            Failure(scenario, "No unique distribution pin is available for " + product.Id + ".");
            return;
        }

        string path = SafeRelativePath(root, expected.PackagePath);
        Check(scenario, File.Exists(path), "Embedded payload is missing: '" + path + "'.");
        if (File.Exists(path))
        {
            Check(scenario, new FileInfo(path).Length == expected.Size,
                "Embedded payload size mismatch: '" + path + "'.");
            string actualHash = Sha256(path);
            Check(scenario, actualHash == expected.Sha256,
                "Embedded payload SHA-256 mismatch: '" + path + "'.");
            if (actualHash == expected.Sha256 && _deepValidatedDistributionHashes.Add(expected.Sha256))
            {
                VerifyEmbeddedZipStructure(scenario, expected, path);
            }
            VerifyPackagedLicenseFile(scenario, product, expected, root, path);
        }
    }

    private void VerifyPackagedLicenseFile(
        string scenario,
        ProductSpec product,
        DistributionPayload expected,
        string root,
        string archivePath)
    {
        if (product.Id != "invisible-dragon")
        {
            return;
        }
        string licensePath = SafeRelativePath(root, expected.PackageLicensePath);
        Check(scenario, File.Exists(licensePath), "EnergyPlus package license is missing: '" + licensePath + "'.");
        if (!File.Exists(licensePath))
        {
            return;
        }
        Check(scenario, new FileInfo(licensePath).Length == expected.LicenseSize
                && Sha256(licensePath) == expected.LicenseSha256,
            "EnergyPlus package license identity mismatch: '" + licensePath + "'.");
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        ZipArchiveEntry? entry = archive.GetEntry(expected.LicenseEntry);
        Check(scenario, entry is not null, "EnergyPlus archive is missing its pinned LICENSE.txt entry.");
        if (entry is not null)
        {
            using Stream stream = entry.Open();
            Check(scenario, entry.Length == expected.LicenseSize && Sha256(stream) == expected.LicenseSha256,
                "Packaged EnergyPlus LICENSE.txt is not byte-identical to the pinned archive entry.");
        }
    }

    private void VerifyEmbeddedZipStructure(string scenario, DistributionPayload expected, string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expandedBytes = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string name = entry.FullName.Replace('\\', '/');
            string[] parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
            bool unsafeName = string.IsNullOrWhiteSpace(name)
                || (name.Length > 0 && name[0] == '/')
                || Regex.IsMatch(name, "^[A-Za-z]:", RegexOptions.CultureInvariant)
                || Path.IsPathRooted(name)
                || parts.Any(part => part is "." or ".." || part.Contains(':'));
            Check(scenario, !unsafeName, "Unsafe nested distribution ZIP entry: '" + name + "'.");
            Check(scenario, seen.Add(name), "Duplicate nested distribution ZIP entry: '" + name + "'.");
            int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            Check(scenario, unixType != 0xA000, "Symbolic-link nested ZIP entry is forbidden: '" + name + "'.");
            if (!string.IsNullOrEmpty(entry.Name))
            {
                expandedBytes += entry.Length;
                fileNames.Add(entry.Name);
            }
        }
        Check(scenario, archive.Entries.Count <= 20000 && expandedBytes <= 8589934592L,
            "Nested distribution ZIP exceeds safety limits.");

        if (expected.Kind != "weather-archive")
        {
            return;
        }

        ZipArchiveEntry[] epwEntries = archive.Entries.Where(entry =>
            !string.IsNullOrEmpty(entry.Name)
            && !entry.FullName.Replace('\\', '/').Contains('/', StringComparison.Ordinal)
            && Path.GetExtension(entry.Name).Equals(".epw", StringComparison.OrdinalIgnoreCase)).ToArray();
        int fileEntryCount = archive.Entries.Count(entry => !string.IsNullOrEmpty(entry.Name));
        Check(scenario, fileEntryCount == expected.ArchiveEpwCount
                && epwEntries.Length == expected.ArchiveEpwCount,
            "KoreanTMY ZIP must contain exactly 80 root EPW files and nothing else.");

        string metadataPath = SafeRelativePath(_repositoryRoot, expected.MetadataPath);
        Check(scenario, File.Exists(metadataPath), "Weather metadata is missing: '" + metadataPath + "'.");
        if (!File.Exists(metadataPath))
        {
            return;
        }
        string[] lines = File.ReadAllLines(metadataPath, Encoding.UTF8);
        Check(scenario, lines.Length > 1, "Weather metadata CSV is empty.");
        if (lines.Length <= 1)
        {
            return;
        }
        string[] header = ParseCsvLine(lines[0]);
        int column = Array.IndexOf(header, expected.MetadataColumn);
        Check(scenario, column >= 0, "Weather metadata column is missing: '" + expected.MetadataColumn + "'.");
        if (column < 0)
        {
            return;
        }
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines.Skip(1).Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            string[] fields = ParseCsvLine(line);
            if (column < fields.Length && !string.IsNullOrWhiteSpace(fields[column]))
            {
                references.Add(fields[column]);
            }
        }
        Check(scenario, references.Count == expected.MetadataReferencedUniqueEpwCount,
            "Weather metadata unique EPW coverage mismatch: expected 78, found " + references.Count + ".");
        foreach (string reference in references)
        {
            Check(scenario, fileNames.Contains(reference),
                "KoreanTMY ZIP is missing metadata-referenced EPW '" + reference + "'.");
        }
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
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
            Check(scenario, !File.Exists(Path.Combine(payloadRoot, "GonieGonie.InvisibleDragon.Grasshopper.Types.dll")),
                "SimpleDragon-only payload must not contain InvisibleDragon Grasshopper types in " + targetKey + ".");
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
            AssemblyReferenceIdentity[] invisibleGrasshopperReferences = files.Where(IsManagedPayloadFile)
                .Select(ReadAssembly)
                .SelectMany(item => item.References)
                .Where(reference => reference.Name is "GonieGonie.InvisibleDragon.GH"
                    or "GonieGonie.InvisibleDragon.Grasshopper.Types")
                .ToArray();
            Check(scenario, invisibleGrasshopperReferences.Length == 0,
                "SimpleDragon " + targetKey + " still references an InvisibleDragon Grasshopper assembly.");
        }
    }

    private void VerifyYakArchives(string scenario, ProductSpec product, string yakRoot, string stageRoot)
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
            string target = rhino7 ? "rhino7" : "rhino8";
            VerifyArchiveRoot(scenario, product, archive, target, Path.Combine(stageRoot, target));
        }
    }

    private void VerifyArchiveRoot(
        string scenario,
        ProductSpec product,
        ZipArchive archive,
        string target,
        string stageRoot)
    {
        string[] entries = archive.Entries.Where(item => !string.IsNullOrEmpty(item.Name))
            .Select(item => NormalizeArchivePath(item.FullName)).ToArray();
        VerifyForbiddenPaths(scenario, product, entries);
        VerifyEmbeddedPayloadEntry(scenario, product, archive, "Yak archive");
        VerifyArchivePayloadManifest(scenario, product, archive, "yak-stage", "Yak archive");
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

        VerifyArchiveChecksums(
            scenario,
            archive,
            "Yak archive",
            Path.Combine(stageRoot, "checksums.sha256"));

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
        VerifyForbiddenPaths(scenario, product, entries);
        VerifyEmbeddedPayloadEntry(scenario, product, archive, "portable ZIP");
        VerifyArchivePayloadManifest(scenario, product, archive, "portable-plugin", "portable ZIP");
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
        VerifyArchiveChecksums(scenario, archive, "Portable ZIP");
    }

    private void VerifyEmbeddedPayloadEntry(
        string scenario,
        ProductSpec product,
        ZipArchive archive,
        string description)
    {
        DistributionPayload? expected = DistributionFor(product.Id);
        if (expected is null)
        {
            Failure(scenario, "No unique distribution pin is available for " + product.Id + ".");
            return;
        }

        ZipArchiveEntry[] matches = archive.Entries.Where(item =>
            NormalizeArchivePath(item.FullName).Equals(expected.PackagePath, StringComparison.Ordinal)).ToArray();
        Check(scenario, matches.Length == 1,
            description + " must contain the product distribution exactly once at '" + expected.PackagePath + "'.");
        if (matches.Length == 1)
        {
            Check(scenario, matches[0].Length == expected.Size,
                description + " embedded payload size mismatch for " + product.DisplayName + ".");
            using Stream stream = matches[0].Open();
            Check(scenario, Sha256(stream) == expected.Sha256,
                description + " embedded payload SHA-256 mismatch for " + product.DisplayName + ".");
        }
        if (product.Id == "invisible-dragon")
        {
            ZipArchiveEntry[] licenses = archive.Entries.Where(item =>
                NormalizeArchivePath(item.FullName).Equals(expected.PackageLicensePath, StringComparison.Ordinal)).ToArray();
            Check(scenario, licenses.Length == 1,
                description + " must contain the EnergyPlus archive license exactly once at '"
                + expected.PackageLicensePath + "'.");
            if (licenses.Length == 1)
            {
                using Stream licenseStream = licenses[0].Open();
                Check(scenario, licenses[0].Length == expected.LicenseSize
                        && Sha256(licenseStream) == expected.LicenseSha256,
                    description + " EnergyPlus LICENSE.txt differs from the pinned nested archive entry.");
            }
        }
    }

    private void VerifyArchivePayloadManifest(
        string scenario,
        ProductSpec product,
        ZipArchive archive,
        string expectedKind,
        string description)
    {
        ZipArchiveEntry? entry = FindEntry(archive, "package-manifest.json");
        if (entry is null)
        {
            return;
        }
        using Stream stream = entry.Open();
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement package = document.RootElement;
        Check(scenario, package.GetProperty("kind").GetString() == expectedKind,
            description + " payload manifest kind mismatch.");
        JsonElement runtime = package.GetProperty("runtime");
        VerifyRuntimeFlags(scenario, product, runtime, description);
        JsonElement[] embedded = runtime.GetProperty("embeddedPayloads").EnumerateArray().ToArray();
        Check(scenario, embedded.Length == 1,
            description + " must declare exactly one embedded payload.");
        if (embedded.Length == 1)
        {
            VerifyEmbeddedPayloadElement(scenario, product, embedded[0], description);
        }
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
        string stage = Path.Combine(_stageRoot, product.Id, target.Id);
        return target.YakLayout == "flat" ? stage : Path.Combine(stage, framework);
    }

    private void VerifyForbiddenPaths(string scenario, ProductSpec product, IEnumerable<string> paths)
    {
        DistributionPayload? expected = DistributionFor(product.Id);
        foreach (string rawPath in paths)
        {
            string path = rawPath.Replace('\\', '/');
            string lower = path.ToLowerInvariant();
            string name = Path.GetFileName(lower);
            bool isExpectedArchive = expected is not null
                && path.Equals(expected.PackagePath, StringComparison.Ordinal);
            bool isExpectedLicense = expected is not null
                && product.Id == "invisible-dragon"
                && path.Equals(expected.PackageLicensePath, StringComparison.Ordinal);
            bool distributionPath = lower.StartsWith("runtime/", StringComparison.Ordinal)
                || lower.Split('/').Any(part => part is "weather" or "energyplus")
                || name.EndsWith(".zip", StringComparison.Ordinal);
            bool forbidden = (!isExpectedArchive && !isExpectedLicense && distributionPath)
                || lower.Split('/').Any(part => part is "__pycache__" or "python")
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

    private void VerifyArchiveChecksums(
        string scenario,
        ZipArchive archive,
        string description,
        string? baselineChecksumPath = null)
    {
        ZipArchiveEntry[] checksumEntries = archive.Entries.Where(item =>
            NormalizeArchivePath(item.FullName).Equals("checksums.sha256", StringComparison.Ordinal)).ToArray();
        Check(scenario, checksumEntries.Length == 1,
            description + " must contain exactly one checksums.sha256 entry.");
        if (checksumEntries.Length != 1)
        {
            return;
        }
        ZipArchiveEntry checksumEntry = checksumEntries[0];

        string checksumText;
        using (var reader = new StreamReader(checksumEntry.Open(), Encoding.UTF8, true, 1024, leaveOpen: false))
        {
            checksumText = reader.ReadToEnd();
        }
        var listed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in checksumText.Split(NewLineSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = Regex.Match(line, "^(?<hash>[0-9a-f]{64})  (?<path>.+)$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                Failure(scenario, "Invalid checksum line inside " + description + ": '" + line + "'.");
                continue;
            }
            string path = match.Groups["path"].Value;
            bool unique = !listed.ContainsKey(path);
            Check(scenario, unique, description + " checksum inventory contains duplicate path '" + path + "'.");
            if (unique)
            {
                listed.Add(path, match.Groups["hash"].Value);
            }
            ZipArchiveEntry? entry = FindEntry(archive, path);
            Check(scenario, entry is not null, description + " checksum target is missing: '" + path + "'.");
            if (entry is not null)
            {
                using Stream stream = entry.Open();
                Check(scenario, Sha256(stream) == match.Groups["hash"].Value,
                    description + " SHA-256 mismatch for '" + path + "'.");
            }
        }

        string[] expected = archive.Entries.Where(item => !string.IsNullOrEmpty(item.Name))
            .Select(item => NormalizeArchivePath(item.FullName))
            .Where(path => path != "checksums.sha256")
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Check(scenario, listed.Keys.OrderBy(path => path, StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal),
            description + " checksum inventory is incomplete or contains extra paths.");

        if (baselineChecksumPath is null)
        {
            return;
        }
        Check(scenario, File.Exists(baselineChecksumPath),
            description + " baseline checksum file is missing: '" + baselineChecksumPath + "'.");
        if (!File.Exists(baselineChecksumPath))
        {
            return;
        }

        var baseline = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(baselineChecksumPath))
        {
            Match match = Regex.Match(line, "^(?<hash>[0-9a-f]{64})  (?<path>.+)$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                Failure(scenario, "Invalid checksum line in archive baseline '" + baselineChecksumPath + "': '" + line + "'.");
                continue;
            }
            string path = match.Groups["path"].Value;
            bool unique = !baseline.ContainsKey(path);
            Check(scenario, unique,
                description + " baseline checksum inventory contains duplicate path '" + path + "'.");
            if (unique)
            {
                baseline.Add(path, match.Groups["hash"].Value);
            }
        }

        Check(scenario,
            baseline.Keys.OrderBy(path => path, StringComparer.Ordinal)
                .SequenceEqual(listed.Keys.OrderBy(path => path, StringComparer.Ordinal), StringComparer.Ordinal),
            description + " checksum inventory differs from its pre-Yak stage baseline.");
        foreach ((string path, string hash) in baseline)
        {
            if (path == "manifest.yml" || !listed.TryGetValue(path, out string? archiveHash))
            {
                continue;
            }
            Check(scenario, archiveHash == hash,
                description + " checksum for '" + path + "' differs from its pre-Yak stage baseline.");
        }
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

    [JsonPropertyName("publication")]
    public PublicationSpec Publication { get; set; } = new();

    [JsonPropertyName("targets")]
    public List<TargetSpec> Targets { get; set; } = new();

    [JsonPropertyName("shared_assemblies")]
    public List<string> SharedAssemblies { get; set; } = new();

    [JsonPropertyName("products")]
    public List<ProductSpec> Products { get; set; } = new();
}

internal sealed class PublicationSpec
{
    [JsonPropertyName("projectLicense")]
    public string ProjectLicense { get; set; } = string.Empty;

    [JsonPropertyName("projectLicenseOwner")]
    public string ProjectLicenseOwner { get; set; } = string.Empty;

    [JsonPropertyName("projectLicenseOwnerType")]
    public string ProjectLicenseOwnerType { get; set; } = string.Empty;

    [JsonPropertyName("projectLicenseReview")]
    public string ProjectLicenseReview { get; set; } = string.Empty;

    [JsonPropertyName("publicSupportEmail")]
    public string PublicSupportEmail { get; set; } = string.Empty;

    [JsonPropertyName("publicSupportEmailReview")]
    public string PublicSupportEmailReview { get; set; } = string.Empty;

    [JsonPropertyName("weatherSource")]
    public string WeatherSource { get; set; } = string.Empty;

    [JsonPropertyName("weatherRedistributionStatus")]
    public string WeatherRedistributionStatus { get; set; } = string.Empty;
}

internal sealed class DistributionManifest
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("payloads")]
    public List<DistributionPayload> Payloads { get; set; } = new();
}

internal sealed class DistributionPayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("product")]
    public string Product { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("packagePath")]
    public string PackagePath { get; set; } = string.Empty;

    [JsonPropertyName("developmentPath")]
    public string DevelopmentPath { get; set; } = string.Empty;

    [JsonPropertyName("origin")]
    public DistributionOrigin Origin { get; set; } = new();

    [JsonPropertyName("archiveEpwCount")]
    public int ArchiveEpwCount { get; set; }

    [JsonPropertyName("metadataReferencedUniqueEpwCount")]
    public int MetadataReferencedUniqueEpwCount { get; set; }

    [JsonPropertyName("metadataPath")]
    public string MetadataPath { get; set; } = string.Empty;

    [JsonPropertyName("metadataColumn")]
    public string MetadataColumn { get; set; } = string.Empty;

    [JsonPropertyName("licenseEntry")]
    public string LicenseEntry { get; set; } = string.Empty;

    [JsonPropertyName("packageLicensePath")]
    public string PackageLicensePath { get; set; } = string.Empty;

    [JsonPropertyName("licenseSize")]
    public long LicenseSize { get; set; }

    [JsonPropertyName("licenseSha256")]
    public string LicenseSha256 { get; set; } = string.Empty;
}

internal sealed class DistributionOrigin
{
    [JsonPropertyName("site")]
    public string Site { get; set; } = string.Empty;

    [JsonPropertyName("dataset")]
    public string Dataset { get; set; } = string.Empty;

    [JsonPropertyName("sourcePage")]
    public string SourcePage { get; set; } = string.Empty;

    [JsonPropertyName("southKoreaIndex")]
    public string SouthKoreaIndex { get; set; } = string.Empty;

    [JsonPropertyName("citation")]
    public string Citation { get; set; } = string.Empty;

    [JsonPropertyName("solarDataSource")]
    public string SolarDataSource { get; set; } = string.Empty;

    [JsonPropertyName("solarDataProvider")]
    public string SolarDataProvider { get; set; } = string.Empty;

    [JsonPropertyName("copernicusLicense")]
    public string CopernicusLicense { get; set; } = string.Empty;

    [JsonPropertyName("oikolabTerms")]
    public string OikolabTerms { get; set; } = string.Empty;

    [JsonPropertyName("reviewedAt")]
    public string ReviewedAt { get; set; } = string.Empty;

    [JsonPropertyName("weatherRedistributionStatus")]
    public string WeatherRedistributionStatus { get; set; } = string.Empty;
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
