using Dragons.BuildingEnergy.Contracts;
using Dragons.EnergyPlus.Runtime;
using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Shape;
using Dragons.InvisibleDragon.Tests.Model;
using OpaqueConstruction = Dragons.InvisibleDragon.Construction.Construction;

namespace Dragons.InvisibleDragon.Tests.Hvac;

public sealed class ColdSourceEnergyPlusIntegrationTests
{
    [EnergyPlusRunnableModelTests.EnergyPlusGeneratedModelFact]
    public async Task StandaloneElectricChillerPlantRunsInEnergyPlus242()
    {
        var tower = new OpenSingleSpeedCoolingTower(
            new EntityId("RUN-COLD-TOWER"),
            "Runnable cooling tower",
            nominalCapacityWatts: 100_000);
        var chiller = new Chiller(
            new EntityId("RUN-COLD-CHILLER"),
            "Runnable chiller",
            3.2,
            CompressorType.Turbo,
            tower,
            nominalCapacityWatts: 80_000);

        await RunSourceAsync(chiller, "electric-chiller");
    }

    [EnergyPlusRunnableModelTests.EnergyPlusGeneratedModelFact]
    public async Task StandaloneScrewChillerBicubicPlantRunsInEnergyPlus242()
    {
        var tower = new OpenSingleSpeedCoolingTower(
            new EntityId("RUN-SCREW-TOWER"),
            "Runnable screw cooling tower",
            nominalCapacityWatts: 100_000);
        var chiller = new Chiller(
            new EntityId("RUN-SCREW-CHILLER"),
            "Runnable screw chiller",
            3.2,
            CompressorType.Screw,
            tower,
            nominalCapacityWatts: 80_000);

        await RunSourceAsync(chiller, "screw-chiller-bicubic");
    }

    [EnergyPlusRunnableModelTests.EnergyPlusGeneratedModelFact]
    public async Task AbsorptionChillerGeneratorPlantRunsInEnergyPlus242()
    {
        var boiler = new Boiler(
            new EntityId("RUN-ABS-BOILER"),
            "Runnable absorption generator",
            Fuel.NaturalGas,
            nominalThermalEfficiency: 0.88,
            nominalCapacityWatts: 120_000,
            setpointTemperatureCelsius: 80);
        var tower = new OpenSingleSpeedCoolingTower(
            new EntityId("RUN-ABS-TOWER"),
            "Runnable absorption tower",
            nominalCapacityWatts: 100_000);
        var chiller = new AbsorptionChiller(
            new EntityId("RUN-ABS-CHILLER"),
            "Runnable absorption chiller",
            0.72,
            boiler,
            tower,
            nominalCapacityWatts: 80_000);

        await RunSourceAsync(chiller, "absorption-chiller");
    }

    private static async Task RunSourceAsync(SourceSystem source, string caseName)
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
        string inputDirectory = Path.Combine(
            repository,
            "temp",
            "integration",
            "invisible-cold-source",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inputDirectory);
        string input = Path.Combine(inputDirectory, $"{caseName}.idf");
        try
        {
            IddSchema schema = IddParser.ParseFile(resolution.Runtime!.IddPath);
            IdfDocument document = CreateRunnableBoxModel().ToIdfDocument(schema);
            Append(document, source.ToIdfObjects(new IdfGenerationContext(schema)));
            document.ApplyDefaults();
            ValidationResult validation = IdfValidator.Validate(
                document,
                new IdfValidationOptions { ValidateSchemaDefaults = false });
            Assert.True(
                validation.IsValid,
                string.Join(
                    Environment.NewLine,
                    validation.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
            IdfWriter.WriteFile(input, document);

            string weather = Path.Combine(
                runtimeRoot,
                "WeatherData",
                "USA_IL_Chicago-OHare.Intl.AP.725300_TMY3.epw");
            EnergyPlusRunResult result = await new EnergyPlusRunner().RunAsync(
                new EnergyPlusRunRequest(
                    resolution.Runtime,
                    input,
                    weather,
                    Path.Combine(repository, "temp", "integration", "invisible-cold-source-runs"))
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

    private static EnergyModel CreateRunnableBoxModel()
    {
        var material = new Material("Cold source concrete", 1.4, 2200, 880);
        var construction = new OpaqueConstruction(
            "Cold source envelope",
            new[] { new Layer("Cold source concrete layer", material, 0.2) });
        Surface[] surfaces =
        {
            Surface("COLD-FLOOR", "Cold source floor", SurfaceType.Floor, SurfaceBoundary.Ground, construction, new[]
            {
                new Vertex(0, 0, 0), new Vertex(0, 6, 0), new Vertex(8, 6, 0), new Vertex(8, 0, 0),
            }),
            Surface("COLD-ROOF", "Cold source roof", SurfaceType.Ceiling, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(0, 0, 3), new Vertex(8, 0, 3), new Vertex(8, 6, 3), new Vertex(0, 6, 3),
            }),
            Surface("COLD-SOUTH", "Cold source south", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(0, 0, 0), new Vertex(8, 0, 0), new Vertex(8, 0, 3), new Vertex(0, 0, 3),
            }),
            Surface("COLD-NORTH", "Cold source north", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(8, 6, 0), new Vertex(0, 6, 0), new Vertex(0, 6, 3), new Vertex(8, 6, 3),
            }),
            Surface("COLD-WEST", "Cold source west", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(0, 6, 0), new Vertex(0, 0, 0), new Vertex(0, 0, 3), new Vertex(0, 6, 3),
            }),
            Surface("COLD-EAST", "Cold source east", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(8, 0, 0), new Vertex(8, 6, 0), new Vertex(8, 6, 3), new Vertex(8, 0, 3),
            }),
        };
        var zone = new Zone(
            new EntityId("COLD-ZONE"),
            "Cold source zone",
            surfaces,
            TestDomainFactory.EmptyProfile("COLD-PROFILE"),
            infiltrationAirChangesPerHour: 0.3);
        return new EnergyModel("Runnable cold source box", new[] { zone });
    }

    private static Surface Surface(
        string id,
        string name,
        SurfaceType type,
        SurfaceBoundary boundary,
        OpaqueConstruction construction,
        IEnumerable<Vertex> vertices) => new(
            new EntityId(id),
            name,
            type,
            construction,
            boundary,
            new PlanarPolygon(vertices));

    private static void Append(IdfDocument document, IEnumerable<IdfObject> objects)
    {
        foreach (IdfObject item in objects)
        {
            document.Append(item);
        }
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

    private static string FormatFailure(EnergyPlusRunResult result) => string.Join(
        Environment.NewLine,
        result.Failure?.Message,
        result.Failure?.Detail,
        result.Outputs.Error?.TextContent,
        result.ExpandObjectsProcess?.StandardError,
        result.EnergyPlusProcess?.StandardError);
}
