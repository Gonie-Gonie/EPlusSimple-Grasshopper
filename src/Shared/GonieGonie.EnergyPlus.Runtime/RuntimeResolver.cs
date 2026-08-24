namespace GonieGonie.EnergyPlus.Runtime;

/// <summary>
/// Finds EnergyPlus 24.2 and verifies both its manifest and executable payload hashes.
/// </summary>
public sealed class RuntimeResolver
{
    private static readonly string[] EnvironmentVariableNames =
    {
        "GONIEGONIE_ENERGYPLUS_ROOT",
        "ENERGYPLUS_24_2_ROOT",
        "ENERGYPLUS_ROOT"
    };

    private readonly EnergyPlusRuntimeManifest expectedManifest;

    public RuntimeResolver()
        : this(EnergyPlusRuntimeManifest.Supported)
    {
    }

    public RuntimeResolver(EnergyPlusRuntimeManifest expectedManifest)
    {
        this.expectedManifest = expectedManifest
            ?? throw new ArgumentNullException(nameof(expectedManifest));

        var errors = expectedManifest.Validate();
        if (errors.Count != 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(expectedManifest));
        }
    }

    /// <summary>
    /// Resolves and hash-verifies the configured runtime without throwing for expected configuration failures.
    /// </summary>
    public async Task<EnergyPlusRuntimeResolution> ResolveAsync(
        EnergyPlusRuntimeResolveOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            return Failure(
                EnergyPlusFailureCategory.UserInput,
                "RESOLVE_OPTIONS_REQUIRED",
                "Runtime resolution options are required.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifestResolution = ResolveManifest(options.ManifestPath);
            if (manifestResolution.Failure is not null)
            {
                return new EnergyPlusRuntimeResolution(null, manifestResolution.Failure, Array.Empty<string>());
            }

            var rootResolution = ResolveCandidateRoots(options);
            if (rootResolution.Failure is not null)
            {
                return new EnergyPlusRuntimeResolution(null, rootResolution.Failure, rootResolution.Roots);
            }

            EnergyPlusFailure? integrityFailure = null;
            foreach (var root in rootResolution.Roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(root))
                {
                    continue;
                }

                var candidate = await VerifyCandidateAsync(root, cancellationToken).ConfigureAwait(false);
                if (candidate.Runtime is not null)
                {
                    return new EnergyPlusRuntimeResolution(candidate.Runtime, null, rootResolution.Roots);
                }

                integrityFailure = candidate.Failure;
                if (options.RuntimeRoot is not null)
                {
                    break;
                }
            }

            if (integrityFailure is not null)
            {
                return new EnergyPlusRuntimeResolution(null, integrityFailure, rootResolution.Roots);
            }

            return new EnergyPlusRuntimeResolution(
                null,
                new EnergyPlusFailure(
                    EnergyPlusFailureCategory.RuntimeNotFound,
                    "RUNTIME_NOT_FOUND",
                    "A compatible EnergyPlus 24.2.0 installation was not found.",
                    rootResolution.Roots.Count == 0
                        ? "No runtime search roots were configured."
                        : "Checked: " + string.Join(", ", rootResolution.Roots)),
                rootResolution.Roots);
        }
        catch (OperationCanceledException)
        {
            return Failure(
                EnergyPlusFailureCategory.Cancelled,
                "RUNTIME_RESOLUTION_CANCELLED",
                "EnergyPlus runtime resolution was cancelled.");
        }
        catch (Exception exception)
        {
            return new EnergyPlusRuntimeResolution(
                null,
                EnergyPlusFailure.Internal(
                    "RUNTIME_RESOLUTION_INTERNAL",
                    "An unexpected error occurred while resolving EnergyPlus.",
                    exception),
                Array.Empty<string>());
        }
    }

    private (EnergyPlusRuntimeManifest? Manifest, EnergyPlusFailure? Failure) ResolveManifest(string? manifestPath)
    {
        if (manifestPath is null)
        {
            return (expectedManifest, null);
        }

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.UserInput,
                "MANIFEST_PATH_INVALID",
                "The runtime manifest path cannot be empty."));
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(manifestPath);
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.UserInput,
                "MANIFEST_PATH_INVALID",
                "The runtime manifest path is invalid.",
                exception.Message));
        }

        if (!File.Exists(fullPath))
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.UserInput,
                "MANIFEST_NOT_FOUND",
                "The requested runtime manifest does not exist.",
                fullPath));
        }

        try
        {
            var manifest = EnergyPlusRuntimeManifest.Load(fullPath);
            var differences = manifest.CompareWith(expectedManifest);
            if (differences.Count != 0)
            {
                return (null, new EnergyPlusFailure(
                    EnergyPlusFailureCategory.RuntimeIntegrity,
                    "MANIFEST_MISMATCH",
                    "The runtime manifest does not match the pinned EnergyPlus payload.",
                    string.Join(" ", differences)));
            }

            return (manifest, null);
        }
        catch (InvalidDataException exception)
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.RuntimeIntegrity,
                "MANIFEST_INVALID",
                "The runtime manifest is invalid.",
                exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.UserInput,
                "MANIFEST_UNREADABLE",
                "The runtime manifest cannot be read.",
                exception.Message));
        }
    }

    private (IReadOnlyList<string> Roots, EnergyPlusFailure? Failure) ResolveCandidateRoots(
        EnergyPlusRuntimeResolveOptions options)
    {
        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        EnergyPlusFailure? AddCallerRoot(string candidate, string source)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return new EnergyPlusFailure(
                    EnergyPlusFailureCategory.UserInput,
                    "RUNTIME_ROOT_INVALID",
                    $"The {source} runtime root cannot be empty.");
            }

            try
            {
                var normalized = RuntimeFileSystem.NormalizeDirectory(candidate);
                if (seen.Add(normalized))
                {
                    roots.Add(normalized);
                }

                return null;
            }
            catch (Exception exception) when (IsPathException(exception))
            {
                return new EnergyPlusFailure(
                    EnergyPlusFailureCategory.UserInput,
                    "RUNTIME_ROOT_INVALID",
                    $"The {source} runtime root is invalid.",
                    exception.Message);
            }
        }

        if (options.RuntimeRoot is not null)
        {
            var failure = AddCallerRoot(options.RuntimeRoot, "explicit");
            return (roots, failure);
        }

        foreach (var additionalRoot in options.AdditionalSearchRoots ?? Array.Empty<string>())
        {
            var failure = AddCallerRoot(additionalRoot, "additional");
            if (failure is not null)
            {
                return (roots, failure);
            }
        }

        if (options.SearchDefaultCacheLocation)
        {
            if (options.CachedRuntimeRoot is not null)
            {
                var failure = AddCallerRoot(options.CachedRuntimeRoot, "cached");
                if (failure is not null)
                {
                    return (roots, failure);
                }
            }
            else
            {
                try
                {
                    var cachedRoot = EnergyPlusRuntimePaths.GetDefaultRuntimeRoot(expectedManifest);
                    if (seen.Add(cachedRoot))
                    {
                        roots.Add(cachedRoot);
                    }
                }
                catch (Exception exception) when (
                    IsPathException(exception) || exception is InvalidOperationException)
                {
                    // Profile-based discovery is optional. Explicit and additional roots remain usable.
                }
            }
        }

        if (options.SearchEnvironmentVariables)
        {
            foreach (var variableName in EnvironmentVariableNames)
            {
                var environmentRoot = Environment.GetEnvironmentVariable(variableName);
                if (string.IsNullOrWhiteSpace(environmentRoot))
                {
                    continue;
                }

                try
                {
                    var normalized = RuntimeFileSystem.NormalizeDirectory(environmentRoot);
                    if (seen.Add(normalized))
                    {
                        roots.Add(normalized);
                    }
                }
                catch (Exception exception) when (IsPathException(exception))
                {
                    // Machine-level discovery hints are ignored when malformed; explicit caller paths are not.
                }
            }
        }

        if (options.SearchDefaultInstallLocation)
        {
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrWhiteSpace(systemDrive))
            {
                var conventionalRoot = Path.Combine(systemDrive, "EnergyPlusV24-2-0");
                if (seen.Add(conventionalRoot))
                {
                    roots.Add(conventionalRoot);
                }
            }
        }

        return (roots, null);
    }

    private async Task<EnergyPlusRuntimeResolution> VerifyCandidateAsync(
        string root,
        CancellationToken cancellationToken)
    {
        var energyPlusPath = RuntimeFileSystem.CombineUnder(root, "energyplus.exe");
        var expandObjectsPath = RuntimeFileSystem.CombineUnder(root, "ExpandObjects.exe");
        var iddPath = RuntimeFileSystem.CombineUnder(root, "Energy+.idd");
        var requiredFiles = new[]
        {
            (Path: energyPlusPath, ExpectedHash: expectedManifest.EnergyPlusExecutableSha256, Name: "energyplus.exe"),
            (Path: expandObjectsPath, ExpectedHash: expectedManifest.ExpandObjectsSha256, Name: "ExpandObjects.exe"),
            (Path: iddPath, ExpectedHash: expectedManifest.EnergyPlusIddSha256, Name: "Energy+.idd")
        };

        foreach (var file in requiredFiles)
        {
            if (!File.Exists(file.Path))
            {
                return new EnergyPlusRuntimeResolution(
                    null,
                    new EnergyPlusFailure(
                        EnergyPlusFailureCategory.RuntimeIntegrity,
                        "RUNTIME_FILE_MISSING",
                        $"The EnergyPlus runtime is missing {file.Name}.",
                        root),
                    new[] { root });
            }

            string actualHash;
            try
            {
                actualHash = await RuntimeFileSystem.ComputeSha256Async(file.Path, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return new EnergyPlusRuntimeResolution(
                    null,
                    new EnergyPlusFailure(
                        EnergyPlusFailureCategory.RuntimeIntegrity,
                        "RUNTIME_FILE_UNREADABLE",
                        $"The EnergyPlus runtime file {file.Name} cannot be read.",
                        exception.Message),
                    new[] { root });
            }

            if (!string.Equals(actualHash, file.ExpectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return new EnergyPlusRuntimeResolution(
                    null,
                    new EnergyPlusFailure(
                        EnergyPlusFailureCategory.RuntimeIntegrity,
                        "RUNTIME_HASH_MISMATCH",
                        $"The SHA-256 hash of {file.Name} does not match the pinned runtime.",
                        $"Expected {file.ExpectedHash}; actual {actualHash}."),
                    new[] { root });
            }
        }

        var runtime = new EnergyPlusRuntimeLayout(
            root,
            energyPlusPath,
            expandObjectsPath,
            iddPath,
            expectedManifest,
            DateTimeOffset.UtcNow);
        return new EnergyPlusRuntimeResolution(runtime, null, new[] { root });
    }

    private static EnergyPlusRuntimeResolution Failure(
        EnergyPlusFailureCategory category,
        string code,
        string message)
    {
        return new EnergyPlusRuntimeResolution(
            null,
            new EnergyPlusFailure(category, code, message),
            Array.Empty<string>());
    }

    private static bool IsPathException(Exception exception)
    {
        return exception is ArgumentException
            || exception is NotSupportedException
            || exception is PathTooLongException;
    }
}
