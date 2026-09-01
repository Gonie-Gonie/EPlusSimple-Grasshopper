using System.Security;
using System.Security.Cryptography;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Grasshopper.Parameters;
using Dragons.InvisibleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace Dragons.InvisibleDragon.Grasshopper.Components;

/// <summary>
/// Verifies an explicitly selected EPW for standalone InvisibleDragon execution.
/// </summary>
public sealed class VerifyInvisibleDragonWeatherComponent : DragonComponent
{
    private const string DiagnosticPrefix = "INVISIBLEDRAGON.GH.WEATHER.";

    public VerifyInvisibleDragonWeatherComponent()
        : base(
            "Verify InvisibleDragon Weather",
            "ID Weather",
            "Verifies a deliberately selected local EPW for standalone InvisibleDragon execution. Relative locations are resolved from a saved Grasshopper document, and local paths are not exposed.",
            DragonPanels.Core)
    {
    }

    public override Guid ComponentGuid => new("4f443564-2e13-4a79-8845-27d1e6eb285d");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter(
            "EPW File",
            "EPW",
            "Absolute EPW file location, or a relative location resolved from the saved Grasshopper document.",
            GH_ParamAccess.item);
        pManager[0].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new PreparedWeatherFileParam(),
            "Weather",
            "Weather",
            "Opaque, content-addressed weather handle for Run InvisibleDragon.",
            GH_ParamAccess.item);
        pManager.AddBooleanParameter(
            "Success",
            "OK",
            "True when the selected EPW was verified.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new DiagnosticParam(),
            "Diagnostics",
            "D",
            "Path-free EPW verification diagnostics.",
            GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string? epwInput = null;
        if (!DA.GetData(0, ref epwInput) || string.IsNullOrWhiteSpace(epwInput))
        {
            DA.SetData(1, false);
            DA.SetDataList(2, Enumerable.Empty<DiagnosticGoo>());
            return;
        }

        GH_Document? document = OnPingDocument();
        string? documentFilePath = document is not null && document.IsFilePathDefined
            ? document.FilePath
            : null;
        WeatherVerificationResult result = VerifyInput(epwInput, documentFilePath);
        if (result.Weather is not null)
        {
            DA.SetData(0, new PreparedWeatherFileGoo(result.Weather));
        }

        Report(result.Diagnostics);
        DA.SetData(1, result.Success);
        DA.SetDataList(2, result.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }

    internal static WeatherVerificationResult VerifyInput(
        string? epwInput,
        string? documentFilePath)
    {
        if (string.IsNullOrWhiteSpace(epwInput))
        {
            return WeatherVerificationResult.NoInput;
        }

        string input = epwInput!.Trim();
        string resolvedPath;
        try
        {
            if (Path.IsPathRooted(input))
            {
                resolvedPath = Path.GetFullPath(input);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(documentFilePath)
                    || !Path.IsPathRooted(documentFilePath!.Trim()))
                {
                    return Failure(
                        "RELATIVE_PATH_REQUIRES_SAVED_DOCUMENT",
                        "A relative EPW input requires a saved Grasshopper document.",
                        "Save the Grasshopper document and recompute ID Weather, or select an absolute EPW file location.");
                }

                string documentPath = Path.GetFullPath(documentFilePath.Trim());
                string? documentDirectory = Path.GetDirectoryName(documentPath);
                if (string.IsNullOrWhiteSpace(documentDirectory))
                {
                    return Failure(
                        "RELATIVE_PATH_REQUIRES_SAVED_DOCUMENT",
                        "A relative EPW input requires a saved Grasshopper document.",
                        "Save the Grasshopper document and recompute ID Weather, or select an absolute EPW file location.");
                }

                resolvedPath = Path.GetFullPath(Path.Combine(documentDirectory!, input));
            }

            if (!string.Equals(
                    Path.GetExtension(resolvedPath),
                    ".epw",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    "EXTENSION_INVALID",
                    "The selected weather file must use the .epw extension.",
                    "Select an EnergyPlus weather file and recompute ID Weather.");
            }

            if (!File.Exists(resolvedPath))
            {
                return Failure(
                    "FILE_NOT_FOUND",
                    "The selected EPW file could not be found.",
                    "Confirm that the weather file exists and recompute ID Weather.");
            }

            PreparedWeatherFile weather = PreparedWeatherFile.FromVerifiedArtifact(
                resolvedPath,
                "InvisibleDragon local EPW",
                Path.GetFileName(resolvedPath));
            return WeatherVerificationResult.Verified(weather);
        }
        catch (InvalidDataException)
        {
            return Failure(
                "HEADER_INVALID",
                "The selected file does not begin with a valid EnergyPlus LOCATION header.",
                "Select a valid EPW file and recompute ID Weather.");
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or SecurityException
            or NotSupportedException
            or CryptographicException)
        {
            return Failure(
                "READ_FAILED",
                "The selected EPW file could not be safely read and verified.",
                "Confirm that the file is accessible and recompute ID Weather.");
        }
    }

    private static WeatherVerificationResult Failure(
        string code,
        string message,
        string suggestedAction)
    {
        return WeatherVerificationResult.Failed(new Diagnostic(
            DiagnosticPrefix + code,
            DiagnosticSeverity.Error,
            message,
            suggestedAction: suggestedAction));
    }
}

internal sealed class WeatherVerificationResult
{
    private WeatherVerificationResult(
        PreparedWeatherFile? weather,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        Weather = weather;
        Diagnostics = diagnostics;
    }

    internal static WeatherVerificationResult NoInput { get; } =
        new(null, Array.Empty<Diagnostic>());

    public PreparedWeatherFile? Weather { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Success => Weather is not null && !Diagnostics.Any(item => item.IsFailure);

    internal static WeatherVerificationResult Verified(PreparedWeatherFile weather) =>
        new(weather, Array.Empty<Diagnostic>());

    internal static WeatherVerificationResult Failed(Diagnostic diagnostic) =>
        new(null, new[] { diagnostic });
}
