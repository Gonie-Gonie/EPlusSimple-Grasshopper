using Dragons.BuildingEnergy.Contracts;
using Dragons.EnergyPlus.Runtime;
using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Shape;
using OpaqueConstruction = Dragons.InvisibleDragon.Construction.Construction;

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class EnergyPlusRunnableModelTests
{
    [EnergyPlusGeneratedModelFact]
    public async Task GeneratedSingleZoneIdealLoadsModelRunsInEnergyPlus242()
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
        string inputDirectory = Path.Combine(repository, "temp", "integration", "invisible-generated-model", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inputDirectory);
        string input = Path.Combine(inputDirectory, "single-zone.idf");
        try
        {
            IddSchema schema = IddParser.ParseFile(resolution.Runtime!.IddPath);
            IdfDocument document = CreateRunnableBoxModel().ToIdfDocument(schema);
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
                    Path.Combine(repository, "temp", "integration", "invisible-generated-model-runs"))
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
        var material = new Material("Runnable concrete", 1.4, 2200, 880);
        var construction = new OpaqueConstruction(
            "Runnable envelope",
            new[] { new Layer("Runnable concrete layer", material, 0.2) });
        Surface[] surfaces =
        {
            Surface("RUN-FLOOR", "Runnable Floor", SurfaceType.Floor, SurfaceBoundary.Ground, construction, new[]
            {
                new Vertex(0, 0, 0), new Vertex(0, 6, 0), new Vertex(8, 6, 0), new Vertex(8, 0, 0),
            }),
            Surface("RUN-ROOF", "Runnable Roof", SurfaceType.Ceiling, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(0, 0, 3), new Vertex(8, 0, 3), new Vertex(8, 6, 3), new Vertex(0, 6, 3),
            }),
            Surface("RUN-SOUTH", "Runnable South", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(0, 0, 0), new Vertex(8, 0, 0), new Vertex(8, 0, 3), new Vertex(0, 0, 3),
            }),
            Surface("RUN-NORTH", "Runnable North", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(8, 6, 0), new Vertex(0, 6, 0), new Vertex(0, 6, 3), new Vertex(8, 6, 3),
            }),
            Surface("RUN-WEST", "Runnable West", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(0, 6, 0), new Vertex(0, 0, 0), new Vertex(0, 0, 3), new Vertex(0, 6, 3),
            }),
            Surface("RUN-EAST", "Runnable East", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(8, 0, 0), new Vertex(8, 6, 0), new Vertex(8, 6, 3), new Vertex(8, 0, 3),
            }),
        };
        var zone = new Zone(
            new EntityId("RUN-ZONE"),
            "Runnable Zone",
            surfaces,
            TestDomainFactory.EmptyProfile("RUN-PROFILE"),
            infiltrationAirChangesPerHour: 0.3);
        return new EnergyModel("Runnable InvisibleDragon Box", new[] { zone });
    }

    private static Surface Surface(
        string id,
        string name,
        SurfaceType type,
        SurfaceBoundary boundary,
        OpaqueConstruction construction,
        IEnumerable<Vertex> vertices) =>
        new(new EntityId(id), name, type, construction, boundary, new PlanarPolygon(vertices));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static string FormatFailure(EnergyPlusRunResult result) => string.Join(
        Environment.NewLine,
        result.Failure?.Message,
        result.Failure?.Detail,
        result.Outputs.Error?.TextContent,
        result.ExpandObjectsProcess?.StandardError,
        result.EnergyPlusProcess?.StandardError);

    public sealed class EnergyPlusGeneratedModelFactAttribute : FactAttribute
    {
        public EnergyPlusGeneratedModelFactAttribute()
        {
            if (!string.Equals(
                Environment.GetEnvironmentVariable("DRAGONS_RUN_ENERGYPLUS_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
            {
                Skip = "Set DRAGONS_RUN_ENERGYPLUS_INTEGRATION=1 to run the generated model in EnergyPlus 24.2.";
            }
        }
    }
}
