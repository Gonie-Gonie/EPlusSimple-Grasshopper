namespace GonieGonie.EnergyPlus.Runtime.Tests;

public sealed class EnergyPlusIntegrationTests
{
    [EnergyPlusIntegrationFact]
    public async Task RunsPinnedEnergyPlus242ExampleAndCollectsOutputs()
    {
        var runtimeRoot = Environment.GetEnvironmentVariable("GONIEGONIE_ENERGYPLUS_ROOT")
            ?? @"C:\EnergyPlusV24-2-0";
        var resolution = await new RuntimeResolver().ResolveAsync(new EnergyPlusRuntimeResolveOptions
        {
            RuntimeRoot = runtimeRoot,
            SearchDefaultInstallLocation = false,
            SearchEnvironmentVariables = false
        });
        Assert.True(resolution.IsSuccess, resolution.Failure?.Detail ?? resolution.Failure?.Message);

        var input = System.IO.Path.Combine(runtimeRoot, "ExampleFiles", "1ZoneUncontrolled.idf");
        var weather = System.IO.Path.Combine(
            runtimeRoot,
            "WeatherData",
            "USA_IL_Chicago-OHare.Intl.AP.725300_TMY3.epw");
        Assert.True(File.Exists(input), input);
        Assert.True(File.Exists(weather), weather);

        var tempRoot = System.IO.Path.Combine(
            TestDirectory.FindRepositoryRoot(),
            "temp",
            "integration",
            "energyplus-runtime");
        var result = await new EnergyPlusRunner().RunAsync(new EnergyPlusRunRequest(
            resolution.Runtime!,
            input,
            weather,
            tempRoot)
        {
            Timeout = TimeSpan.FromMinutes(3),
            CleanupPolicy = EnergyPlusCleanupPolicy.DeleteAlways
        });

        Assert.True(result.IsSuccess, FormatFailure(result));
        Assert.NotNull(result.Outputs.Error);
        Assert.NotNull(result.Outputs.Audit);
        Assert.NotNull(result.Outputs.Boundary);
        Assert.NotNull(result.Outputs.TableCsv);
        Assert.Contains("EnergyPlus Completed Successfully", result.Outputs.Error!.TextContent, StringComparison.Ordinal);
        Assert.False(result.WorkDirectoryRetained);
        Assert.Null(result.CleanupError);
    }

    private static string FormatFailure(EnergyPlusRunResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Failure?.Message,
            result.Failure?.Detail,
            result.ExpandObjectsProcess?.StandardError,
            result.EnergyPlusProcess?.StandardError);
    }

    public sealed class EnergyPlusIntegrationFactAttribute : FactAttribute
    {
        public EnergyPlusIntegrationFactAttribute()
        {
            if (!string.Equals(
                Environment.GetEnvironmentVariable("GONIEGONIE_RUN_ENERGYPLUS_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
            {
                Skip = "Set GONIEGONIE_RUN_ENERGYPLUS_INTEGRATION=1 to run against EnergyPlus 24.2.0.";
            }
        }
    }
}
