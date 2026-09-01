using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dragons.EnergyPlus.Runtime;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Results;
using Dragons.SimpleDragon;

namespace Dragons.CompatibilityRunner;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private static async Task<int> Main(string[] args)
    {
        try
        {
            CommandLine command = CommandLine.Parse(args);
            await EmitAsync(command).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    private static async Task EmitAsync(CommandLine command)
    {
        string repositoryRoot = Path.GetFullPath(command.RepositoryRoot);
        string runtimeRoot = Path.GetFullPath(command.RuntimeRoot);
        string outputRoot = Path.GetFullPath(command.Output);
        CompatibilityManifest manifest = ReadManifest(command.Manifest);
        EnergyPlusRuntimeLayout runtime = await ResolveRuntimeAsync(runtimeRoot).ConfigureAwait(false);
        IddSchema schema = IddParser.ParseFile(runtime.IddPath);
        Directory.CreateDirectory(outputRoot);

        CompatibilityCase[] selected = manifest.Cases
            .Where(item => command.CaseId is null || string.Equals(
                item.Id,
                command.CaseId,
                StringComparison.Ordinal))
            .ToArray();
        if (selected.Length == 0)
        {
            throw new ArgumentException("Unknown compatibility case '" + command.CaseId + "'.");
        }

        foreach (CompatibilityCase item in selected)
        {
            await EmitCaseAsync(
                item,
                manifest,
                repositoryRoot,
                runtime,
                schema,
                outputRoot,
                command.SkipEnergyPlus).ConfigureAwait(false);
        }

        Console.WriteLine(
            "C# compatibility engine emitted " + selected.Length + " case(s) to " + outputRoot);
    }

    private static async Task EmitCaseAsync(
        CompatibilityCase item,
        CompatibilityManifest manifest,
        string repositoryRoot,
        EnergyPlusRuntimeLayout runtime,
        IddSchema schema,
        string outputRoot,
        bool skipEnergyPlus)
    {
        string caseRoot = Path.Combine(outputRoot, item.Id);
        Directory.CreateDirectory(caseRoot);
        string inputPath = ResolveUnder(repositoryRoot, item.InputGrm);
        string weatherPath = ResolveUnder(runtime.RootPath, item.Weather);
        VerifyPinnedSha256(inputPath, item.InputGrmSha256, item.Id + " GRM");
        VerifyPinnedSha256(weatherPath, item.WeatherSha256, item.Id + " weather");
        var produced = new List<string>();

        GreenRetrofitModel model = GrmReader.ReadFile(inputPath).RequireModel();
        string roundTripGrmPath = Path.Combine(caseRoot, "roundtrip.grm");
        GrmWriter.WriteFile(roundTripGrmPath, model);
        produced.Add(roundTripGrmPath);
        GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(model);
        if (!conversion.Success)
        {
            throw new InvalidOperationException(
                "Conversion failed for " + item.Id + ": " + string.Join(
                    Environment.NewLine,
                    conversion.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
        }

        string authoringPath = Path.Combine(caseRoot, "authoring.idf");
        IdfWriter.WriteFile(
            authoringPath,
            conversion.ToIdfDocument(schema),
            new IdfWriterOptions
            {
                IncludeSchemaFieldComments = false,
                NewLine = "\n",
            });
        produced.Add(authoringPath);

        string expandedPath = Path.Combine(caseRoot, "expanded.idf");
        await ExpandAsync(authoringPath, expandedPath, runtime, caseRoot).ConfigureAwait(false);
        produced.Add(expandedPath);

        var resultDiagnostics = new List<object>();
        bool declaresSimulation = item.Stages.Any(stage =>
            string.Equals(stage, "energyplus", StringComparison.Ordinal)
            || string.Equals(stage, "grr", StringComparison.Ordinal)
            || string.Equals(stage, "warnings", StringComparison.Ordinal));
        if (!skipEnergyPlus && declaresSimulation)
        {
            string runRoot = Path.Combine(caseRoot, "csharp-energyplus-work");
            Directory.CreateDirectory(runRoot);
            EnergyPlusRunResult run = await new EnergyPlusRunner().RunAsync(
                new EnergyPlusRunRequest(runtime, authoringPath, weatherPath, runRoot)
                {
                    CleanupPolicy = EnergyPlusCleanupPolicy.KeepAlways,
                    MaximumCapturedArtifactBytes = 128L * 1024L * 1024L,
                    Timeout = TimeSpan.FromMinutes(10),
                }).ConfigureAwait(false);
            if (!run.IsSuccess)
            {
                throw new InvalidOperationException(
                    "EnergyPlus failed for " + item.Id + ": " + run.Failure?.Code + " "
                    + run.Failure?.Message + Environment.NewLine + run.Failure?.Detail);
            }

            EnergyPlusSimulationResult simulation = EnergyPlusResultParser.Parse(run);
            GreenRetrofitResultBuildResult build = GreenRetrofitResultBuilder.Build(
                model,
                simulation,
                new GreenRetrofitResultBuildOptions
                {
                    AllowSevereDiagnostics = true,
                });
            if (!build.Success)
            {
                throw new InvalidOperationException(
                    "GRR construction failed for " + item.Id + ": " + string.Join(
                        Environment.NewLine,
                        build.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
            }

            string grrPath = Path.Combine(caseRoot, "result.grr");
            GrrWriter.WriteFile(grrPath, build.RequireResult());
            produced.Add(grrPath);
            string warningsPath = Path.Combine(caseRoot, "warnings.json");
            WriteJson(warningsPath, WarningDocument(simulation.ErrorLog));
            produced.Add(warningsPath);
            resultDiagnostics.AddRange(build.Diagnostics.Select(diagnostic => new
            {
                diagnostic.Code,
                Severity = diagnostic.Severity.ToString(),
                diagnostic.Message,
            }));

            string? errorPath = run.Outputs.Error?.FullPath;
            if (errorPath is not null && File.Exists(errorPath))
            {
                File.Copy(errorPath, Path.Combine(caseRoot, "energyplus.err"), overwrite: true);
            }
        }

        string metadataPath = Path.Combine(caseRoot, "metadata.json");
        WriteJson(metadataPath, new
        {
            Schema = "dragons.compatibility-engine-output.v1",
            Producer = "csharp-port",
            CaseId = item.Id,
            manifest.UpstreamCommit,
            Inputs = new
            {
                Grm = new { Path = item.InputGrm, Sha256 = Sha256File(inputPath) },
                Weather = new { Path = item.Weather, Sha256 = Sha256File(weatherPath) },
            },
            Runtime = new
            {
                EnergyplusExeSha256 = Sha256File(runtime.EnergyPlusExecutablePath),
                IddSha256 = Sha256File(runtime.IddPath),
                ExpandobjectsSha256 = Sha256File(runtime.ExpandObjectsExecutablePath),
            },
            ConversionDiagnostics = conversion.Diagnostics.Select(diagnostic => new
            {
                diagnostic.Code,
                Severity = diagnostic.Severity.ToString(),
                diagnostic.Message,
            }),
            ResultDiagnostics = resultDiagnostics,
            Outputs = produced.Select(path => new
            {
                Path = Path.GetRelativePath(caseRoot, path).Replace('\\', '/'),
                Bytes = new FileInfo(path).Length,
                Sha256 = Sha256File(path),
            }),
        });
    }

    private static object WarningDocument(EnergyPlusErrorLog errorLog)
    {
        return new
        {
            Schema = "dragons.energyplus-warnings.v1",
            Summary = new
            {
                Warning = errorLog.Summary.WarningCount,
                Severe = errorLog.Summary.SevereCount,
                Fatal = errorLog.Summary.FatalCount,
            },
            Items = errorLog.Diagnostics.Select(item => new
            {
                Severity = item.Severity.ToString(),
                Title = item.Message,
            }),
        };
    }

    private static async Task ExpandAsync(
        string source,
        string destination,
        EnergyPlusRuntimeLayout runtime,
        string caseRoot)
    {
        string work = Path.Combine(caseRoot, "csharp-expand-work");
        if (Directory.Exists(work))
        {
            Directory.Delete(work, recursive: true);
        }
        Directory.CreateDirectory(work);
        File.Copy(source, Path.Combine(work, "in.idf"));
        File.Copy(runtime.IddPath, Path.Combine(work, "Energy+.idd"));
        var startInfo = new ProcessStartInfo
        {
            FileName = runtime.ExpandObjectsExecutablePath,
            WorkingDirectory = work,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("ExpandObjects did not start.");
        string stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        string stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "ExpandObjects failed with " + process.ExitCode + ": " + stdout + stderr);
        }

        string expanded = Path.Combine(work, "expanded.idf");
        File.Copy(File.Exists(expanded) ? expanded : Path.Combine(work, "in.idf"), destination, overwrite: true);
        Directory.Delete(work, recursive: true);
    }

    private static CompatibilityManifest ReadManifest(string path)
    {
        return JsonSerializer.Deserialize<CompatibilityManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Compatibility case manifest is empty.");
    }

    private static async Task<EnergyPlusRuntimeLayout> ResolveRuntimeAsync(string runtimeRoot)
    {
        EnergyPlusRuntimeResolution resolution = await new RuntimeResolver().ResolveAsync(
            new EnergyPlusRuntimeResolveOptions
            {
                RuntimeRoot = runtimeRoot,
                SearchDefaultCacheLocation = false,
                SearchDefaultInstallLocation = false,
                SearchEnvironmentVariables = false,
            }).ConfigureAwait(false);
        return resolution.Runtime ?? throw new InvalidOperationException(
            resolution.Failure?.Code + ": " + resolution.Failure?.Message + " " + resolution.Failure?.Detail);
    }

    private static string ResolveUnder(string root, string relative)
    {
        string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(normalizedRoot, relative));
        if (!result.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Compatibility path escapes its declared root: " + relative);
        }
        if (!File.Exists(result))
        {
            throw new FileNotFoundException("Compatibility input was not found.", result);
        }
        return result;
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void VerifyPinnedSha256(string path, string expected, string identity)
    {
        string actual = Sha256File(path);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Pinned " + identity + " hash mismatch: expected " + expected + ", found " + actual + ".");
        }
    }

    private static void WriteJson(string path, object value)
    {
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(value, JsonOptions) + "\n",
            new UTF8Encoding(false));
    }
}

internal sealed record CompatibilityManifest(
    string Schema,
    string UpstreamCommit,
    EnergyPlusIdentity EnergyPlus,
    CompatibilityTolerances Tolerances,
    IReadOnlyList<CompatibilityCase> Cases);

internal sealed record EnergyPlusIdentity(string Version, string Build);

internal sealed record CompatibilityTolerances(
    double IdfAbsolute,
    double IdfRelative,
    double GrrAbsolute,
    double GrrRelative,
    double NearZero,
    int WarningCountDelta);

internal sealed record CompatibilityCase(
    string Id,
    string InputGrm,
    string InputGrmSha256,
    string Weather,
    string WeatherSha256,
    IReadOnlyList<string> Stages);

internal sealed record CommandLine(
    string RepositoryRoot,
    string RuntimeRoot,
    string Manifest,
    string Output,
    string? CaseId,
    bool SkipEnergyPlus)
{
    internal static CommandLine Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool skipEnergyPlus = false;
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, "--skip-energyplus", StringComparison.OrdinalIgnoreCase))
            {
                skipEnergyPlus = true;
                continue;
            }
            if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new ArgumentException("Invalid compatibility-runner argument '" + argument + "'.");
            }
            values[argument] = args[++index];
        }

        return new CommandLine(
            Required(values, "--repository-root"),
            Required(values, "--runtime-root"),
            Required(values, "--manifest"),
            Required(values, "--output"),
            values.TryGetValue("--case", out string? caseId) ? caseId : null,
            skipEnergyPlus);
    }

    private static string Required(Dictionary<string, string> values, string name)
    {
        return values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("Required argument is missing: " + name);
    }
}
