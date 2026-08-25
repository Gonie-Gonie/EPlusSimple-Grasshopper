using System.Globalization;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class GreenRetrofitEnergyPlusIntegrationTests
{
    private static readonly string[] AllowedWarningFamilies =
    {
        "AirConditioner:VariableRefrigerantFlow",
        "CalculateZoneVolume:",
        "GetDXCoils:",
        "GetHTSurfaceData:",
        "GetVRFInput:",
        "GlycolProps::getDensity:",
        "GlycolProps::getSpecificHeat:",
        "InitVRF:",
        "ProcessScheduleInput:",
        "Processing Monthly Tabular Reports:",
        "Temperature out of range [-100. to 200.]",
        "WetBulb not converged",
    };

    [EnergyPlusConvertedFixtureFact]
    public async Task ConvertedAshraeFixtureRunsInEnergyPlus242()
    {
        string runtimeRoot = Environment.GetEnvironmentVariable("GONIEGONIE_ENERGYPLUS_ROOT")
            ?? @"C:\EnergyPlusV24-2-0";
        EnergyPlusRuntimeResolution resolution = await new RuntimeResolver().ResolveAsync(
            new EnergyPlusRuntimeResolveOptions
            {
                RuntimeRoot = runtimeRoot,
                SearchDefaultInstallLocation = false,
                SearchEnvironmentVariables = false,
            });
        Assert.True(resolution.IsSuccess, resolution.Failure?.Detail ?? resolution.Failure?.Message);

        string repository = FindRepositoryRoot();
        string inputDirectory = Path.Combine(
            repository,
            "temp",
            "integration",
            "simple-converted-fixture",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inputDirectory);
        string inputPath = Path.Combine(inputDirectory, "ashrae-140-modified.idf");
        try
        {
            GreenRetrofitModel source = GrmReader.ReadFile(Fixture(repository)).RequireModel();
            IddSchema schema = IddParser.ParseFile(resolution.Runtime!.IddPath);
            IdfWriter.WriteFile(inputPath, GreenRetrofitConverter.ToIdfDocument(source, schema: schema));
            string weatherPath = Path.Combine(
                runtimeRoot,
                "WeatherData",
                "USA_IL_Chicago-OHare.Intl.AP.725300_TMY3.epw");

            EnergyPlusRunResult result = await new EnergyPlusRunner().RunAsync(
                new EnergyPlusRunRequest(
                    resolution.Runtime,
                    inputPath,
                    weatherPath,
                    Path.Combine(repository, "temp", "integration", "simple-converted-fixture-runs"))
                {
                    Timeout = TimeSpan.FromMinutes(3),
                    CleanupPolicy = EnergyPlusCleanupPolicy.DeleteAlways,
                });

            Assert.True(result.IsSuccess, FormatFailure(result));
            Assert.Equal(0, result.Outputs.ErrorSummary.FatalCount);
            // The pinned Python 0.7 fixture intentionally emits this single severe
            // diagnostic for its 1.003 m / 0.04 W/(m K) material. Retaining the
            // original massive layer is required for simulation-result parity.
            Assert.Equal(1, result.Outputs.ErrorSummary.SevereCount);
            string errorLog = Assert.IsType<string>(result.Outputs.Error?.TextContent);
            Assert.Contains(
                "InitConductionTransferFunctions: Found Material that is too thin and/or too highly conductive",
                errorLog,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Error parsing \"FenestrationSurface:Detailed\"", errorLog, StringComparison.Ordinal);
            Assert.DoesNotContain("carbon_dioxide_generation_rate", errorLog, StringComparison.Ordinal);
            Assert.DoesNotContain("multiple assignments for Zone Equipment", errorLog, StringComparison.Ordinal);
            AssertOnlyExpectedWarningFamilies(errorLog);
            AssertWarningCeilings(errorLog);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
        }
    }

    private static string Fixture(string repository)
    {
        return Path.Combine(
            repository,
            "fixtures",
            "simple-dragon",
            "grm",
            "ASHRAE 140 modified.grm");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static string FormatFailure(EnergyPlusRunResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Failure?.Message,
            result.Failure?.Detail,
            result.Outputs.Error?.TextContent,
            result.ExpandObjectsProcess?.StandardError,
            result.EnergyPlusProcess?.StandardError);
    }

    private static void AssertOnlyExpectedWarningFamilies(string errorLog)
    {
        const string marker = "** Warning **";
        using var reader = new StringReader(errorLog);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            int markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                continue;
            }

            string header = line.Substring(markerIndex + marker.Length).Trim();
            Assert.Contains(
                AllowedWarningFamilies,
                family => header.Contains(family, StringComparison.Ordinal));
        }
    }

    private static void AssertWarningCeilings(string errorLog)
    {
        int totalWarnings = ReportedWarningCount(errorLog);
        int vrfHeatingTemperature = RecurringCount(
            errorLog,
            "Exceeded VRF Heat Pump min/max heating temperature limit");
        int vrfCoolingTemperature = RecurringCount(
            errorLog,
            "Exceeded VRF Heat Pump min/max cooling temperature limit");
        int psychrometricTemperature = RecurringCount(errorLog, "Temperature out of range [-100. to 200.]");
        int waterSpecificHeat = RecurringCount(errorLog, "GlycolProps::getSpecificHeat:");
        int waterDensity = RecurringCount(errorLog, "GlycolProps::getDensity:");
        int wetBulb = RecurringCount(errorLog, "WetBulb not converged");

        Assert.InRange(vrfHeatingTemperature, 0, 800);
        Assert.InRange(vrfCoolingTemperature, 0, 400);
        Assert.InRange(psychrometricTemperature, 0, 5);
        Assert.InRange(waterSpecificHeat, 0, 250);
        Assert.InRange(waterDensity, 0, 850);
        Assert.InRange(wetBulb, 0, 5);
        int recurringWarnings = vrfHeatingTemperature
            + vrfCoolingTemperature
            + psychrometricTemperature
            + waterSpecificHeat
            + waterDensity
            + wetBulb;
        Assert.InRange(totalWarnings - recurringWarnings, 0, 15);
        Assert.InRange(totalWarnings, 0, 2200);
    }

    private static int ReportedWarningCount(string errorLog)
    {
        const string summaryPrefix = "EnergyPlus Completed Successfully-- ";
        const string warningSuffix = " Warning;";
        int summaryIndex = errorLog.LastIndexOf(summaryPrefix, StringComparison.Ordinal);
        Assert.True(summaryIndex >= 0, "EnergyPlus did not emit its successful completion summary.");
        int valueIndex = summaryIndex + summaryPrefix.Length;
        int valueEnd = errorLog.IndexOf(warningSuffix, valueIndex, StringComparison.Ordinal);
        Assert.True(valueEnd >= 0, "EnergyPlus did not report a warning count.");
        return ParseCount(errorLog, valueIndex, valueEnd);
    }

    private static int RecurringCount(string errorLog, string warningFragment)
    {
        const string recurringHeader = "===== Recurring Error Summary =====";
        const string countPrefix = "This error occurred ";
        const string countSuffix = " total times;";
        int recurringIndex = errorLog.IndexOf(recurringHeader, StringComparison.Ordinal);
        if (recurringIndex < 0)
        {
            return 0;
        }

        int warningIndex = errorLog.IndexOf(warningFragment, recurringIndex, StringComparison.Ordinal);
        if (warningIndex < 0)
        {
            return 0;
        }

        int countIndex = errorLog.IndexOf(countPrefix, warningIndex, StringComparison.Ordinal);
        if (countIndex < 0)
        {
            return 0;
        }

        int valueIndex = countIndex + countPrefix.Length;
        int valueEnd = errorLog.IndexOf(countSuffix, valueIndex, StringComparison.Ordinal);
        if (valueEnd < 0)
        {
            return 0;
        }

        return ParseCount(errorLog, valueIndex, valueEnd);
    }

    private static int ParseCount(string source, int valueIndex, int valueEnd)
    {
        return int.Parse(
            source.AsSpan(valueIndex, valueEnd - valueIndex),
            NumberStyles.None,
            CultureInfo.InvariantCulture);
    }

    public sealed class EnergyPlusConvertedFixtureFactAttribute : FactAttribute
    {
        public EnergyPlusConvertedFixtureFactAttribute()
        {
            if (!string.Equals(
                Environment.GetEnvironmentVariable("GONIEGONIE_RUN_ENERGYPLUS_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
            {
                Skip = "Set GONIEGONIE_RUN_ENERGYPLUS_INTEGRATION=1 to run the converted fixture in EnergyPlus 24.2.";
            }
        }
    }
}
