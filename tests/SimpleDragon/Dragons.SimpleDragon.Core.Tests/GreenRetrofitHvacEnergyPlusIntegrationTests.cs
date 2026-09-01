using Dragons.BuildingEnergy.Contracts;
using Dragons.EnergyPlus.Runtime;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;

namespace Dragons.SimpleDragon.Tests;

public sealed class GreenRetrofitHvacEnergyPlusIntegrationTests
{
    [EnergyPlusConvertedHvacTheory]
    [InlineData(SourceSystemType.Chiller, SupplySystemType.FanCoilUnit)]
    [InlineData(SourceSystemType.AbsorptionChiller, SupplySystemType.FanCoilUnit)]
    [InlineData(SourceSystemType.Boiler, SupplySystemType.FanCoilUnit)]
    [InlineData(SourceSystemType.DistrictHeating, SupplySystemType.Radiator)]
    public async Task ConvertedHydronicPairRunsInEnergyPlus242(
        SourceSystemType sourceType,
        SupplySystemType supplyType)
    {
        string runtimeRoot = Environment.GetEnvironmentVariable("DRAGONS_ENERGYPLUS_ROOT")
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
        string caseName = sourceType + "-" + supplyType;
        string inputDirectory = Path.Combine(
            repository,
            "temp",
            "integration",
            "simple-converted-hvac",
            caseName,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inputDirectory);
        string inputPath = Path.Combine(inputDirectory, "model.idf");
        try
        {
            GreenRetrofitConversionResult conversion = Convert(repository, sourceType, supplyType);
            Assert.True(conversion.Success, Describe(conversion));
            IddSchema schema = IddParser.ParseFile(resolution.Runtime!.IddPath);
            IdfWriter.WriteFile(inputPath, conversion.ToIdfDocument(schema: schema));
            string weatherPath = Path.Combine(
                runtimeRoot,
                "WeatherData",
                "USA_IL_Chicago-OHare.Intl.AP.725300_TMY3.epw");

            EnergyPlusRunResult result = await new EnergyPlusRunner().RunAsync(
                new EnergyPlusRunRequest(
                    resolution.Runtime,
                    inputPath,
                    weatherPath,
                    Path.Combine(repository, "temp", "integration", "simple-converted-hvac-runs"))
                {
                    Timeout = TimeSpan.FromMinutes(3),
                    CleanupPolicy = EnergyPlusCleanupPolicy.DeleteAlways,
                });

            Assert.True(result.IsSuccess, FormatFailure(result));
            Assert.Equal(0, result.Outputs.ErrorSummary.FatalCount);
            Assert.Equal(0, result.Outputs.ErrorSummary.SevereCount);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
        }
    }

    private static GreenRetrofitConversionResult Convert(
        string repository,
        SourceSystemType sourceType,
        SupplySystemType supplyType)
    {
        GreenRetrofitModel template = GrmReader.ReadFile(Path.Combine(
            repository,
            "fixtures",
            "simple-dragon",
            "grm",
            "ASHRAE 140 modified.grm")).RequireModel();
        SourceSystem source = CreateSource(sourceType);
        var supply = new SupplySystem(
            "integration supply",
            supplyType,
            source.Id.Value,
            source,
            heatingCapacity: supplyType == SupplySystemType.Radiator ? 8_000d : null,
            id: new EntityId("SUPPLY-INTEGRATION"));
        Zone original = Assert.Single(template.Zones);
        var zone = new Zone(
            original.Name,
            original.FloorNumber,
            original.Height,
            original.Surfaces,
            original.ProfileName,
            original.Profile,
            original.LightDensity,
            new[] { new SupplySystemAssignment(supply.Id.Value, supply) },
            id: original.Id);
        var model = new GreenRetrofitModel(
            "converted " + sourceType + " integration",
            template.NorthAxis,
            template.Address,
            template.Vintage,
            template.IsMultifamilyHousing,
            new[] { new BuildingFloor(zone.FloorNumber, new[] { zone }) },
            template.Materials,
            template.SurfaceConstructions,
            template.FenestrationConstructions,
            new[] { source },
            new[] { supply },
            weather: template.Weather);
        return GreenRetrofitConverter.Convert(model);
    }

    private static SourceSystem CreateSource(SourceSystemType type)
    {
        var id = new EntityId("SOURCE-INTEGRATION");
        return type switch
        {
            SourceSystemType.Chiller => new SourceSystem(
                "chiller",
                type,
                coolingCop: 4.2d,
                coolingCapacity: 12_000d,
                compressorType: CompressorType.Turbo,
                coolingTowerType: CoolingTowerType.Open,
                coolingTowerCapacity: 15_000d,
                coolingTowerControl: CoolingTowerControl.SingleSpeed,
                id: id),
            SourceSystemType.AbsorptionChiller => new SourceSystem(
                "absorption",
                type,
                FuelType.NaturalGas,
                coolingCop: 0.85d,
                coolingCapacity: 12_000d,
                boilerEfficiency: 0.9d,
                id: id),
            SourceSystemType.Boiler => new SourceSystem(
                "boiler",
                type,
                FuelType.NaturalGas,
                heatingCapacity: 12_000d,
                efficiency: 0.9d,
                hotWaterSupply: false,
                id: id),
            SourceSystemType.DistrictHeating => new SourceSystem(
                "district",
                type,
                heatingCapacity: 12_000d,
                hotWaterSupply: false,
                id: id),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root was not found.");
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

    private static string Describe(GreenRetrofitConversionResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(item => item.Code + ": " + item.Message));
    }

    public sealed class EnergyPlusConvertedHvacTheoryAttribute : TheoryAttribute
    {
        public EnergyPlusConvertedHvacTheoryAttribute()
        {
            if (!string.Equals(
                Environment.GetEnvironmentVariable("DRAGONS_RUN_ENERGYPLUS_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
            {
                Skip = "Set DRAGONS_RUN_ENERGYPLUS_INTEGRATION=1 to run converted HVAC cases in EnergyPlus 24.2.";
            }
        }
    }
}
