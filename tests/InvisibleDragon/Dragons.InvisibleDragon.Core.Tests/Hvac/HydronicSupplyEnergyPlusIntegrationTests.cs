using Dragons.BuildingEnergy.Contracts;
using Dragons.EnergyPlus.Runtime;
using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Profile;
using Dragons.InvisibleDragon.Shape;
using OpaqueConstruction = Dragons.InvisibleDragon.Construction.Construction;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

namespace Dragons.InvisibleDragon.Tests.Hvac;

public sealed class HydronicSupplyEnergyPlusIntegrationTests
{
    [HydronicEnergyPlusFact]
    public Task BoilerAndRadiatorModelRunsInEnergyPlus242()
    {
        return RunEnergyPlusAsync(CreateRunnableRadiatorModel(), "boiler-radiator");
    }

    [HydronicEnergyPlusFact]
    public Task BoilerHeatingFanCoilModelRunsInEnergyPlus242()
    {
        return RunEnergyPlusAsync(CreateRunnableHeatingFanCoilModel(), "boiler-heating-fan-coil");
    }

    [HydronicEnergyPlusFact]
    public Task ChillerCoolingFanCoilModelRunsInEnergyPlus242()
    {
        return RunEnergyPlusAsync(CreateRunnableCoolingFanCoilModel(), "chiller-cooling-fan-coil");
    }

    private static async Task RunEnergyPlusAsync(EnergyModel model, string caseName)
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
            "invisible-hydronic-supply",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inputDirectory);
        string input = Path.Combine(inputDirectory, $"{caseName}.idf");
        try
        {
            IddSchema schema = IddParser.ParseFile(resolution.Runtime!.IddPath);
            IdfDocument document = model.ToIdfDocument(schema);
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
                    Path.Combine(repository, "temp", "integration", "invisible-hydronic-supply-runs"))
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

    private static EnergyModel CreateRunnableRadiatorModel()
    {
        Zone zone = CreateRunnableZone();
        var boiler = new Boiler(new EntityId("HYD-BOILER"), "Hydronic Boiler", Fuel.NaturalGas);
        var radiator = new Radiator(
            new EntityId("HYD-RADIATOR"),
            "Hydronic Radiator",
            boiler,
            heatingCapacityWatts: null,
            radiantFraction: 0.2);
        return ModelWith(zone, radiator, "Runnable InvisibleDragon Hydronic Radiator");
    }

    private static EnergyModel CreateRunnableHeatingFanCoilModel()
    {
        Zone zone = CreateRunnableZone();
        var boiler = new Boiler(new EntityId("HYD-FCU-BOILER"), "Hydronic FCU Boiler", Fuel.NaturalGas);
        var fanCoil = new FanCoilUnit(
            new EntityId("HYD-HEATING-FCU"),
            "Hydronic Heating FCU",
            boiler);
        return ModelWith(zone, fanCoil, "Runnable InvisibleDragon Heating Fan Coil");
    }

    private static EnergyModel CreateRunnableCoolingFanCoilModel()
    {
        Zone zone = CreateRunnableZone();
        var tower = new OpenSingleSpeedCoolingTower(
            new EntityId("HYD-FCU-TOWER"),
            "Hydronic FCU Tower");
        var chiller = new Chiller(
            new EntityId("HYD-FCU-CHILLER"),
            "Hydronic FCU Chiller",
            5,
            CompressorType.Turbo,
            tower);
        var fanCoil = new FanCoilUnit(
            new EntityId("HYD-COOLING-FCU"),
            "Hydronic Cooling FCU",
            chiller);
        return ModelWith(zone, fanCoil, "Runnable InvisibleDragon Cooling Fan Coil");
    }

    private static Zone CreateRunnableZone()
    {
        var material = new Material("Hydronic concrete", 1.4, 2200, 880);
        var construction = new OpaqueConstruction(
            "Hydronic envelope",
            new[] { new Layer("Hydronic concrete layer", material, 0.2) });
        Surface[] surfaces =
        {
            Surface("HYD-FLOOR", "Hydronic Floor", SurfaceType.Floor, SurfaceBoundary.Ground, construction, new[]
            {
                new Vertex(0, 0, 0), new Vertex(0, 6, 0), new Vertex(8, 6, 0), new Vertex(8, 0, 0),
            }),
            Surface("HYD-ROOF", "Hydronic Roof", SurfaceType.Ceiling, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(0, 0, 3), new Vertex(8, 0, 3), new Vertex(8, 6, 3), new Vertex(0, 6, 3),
            }),
            Surface("HYD-SOUTH", "Hydronic South", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(0, 0, 0), new Vertex(8, 0, 0), new Vertex(8, 0, 3), new Vertex(0, 0, 3),
            }),
            Surface("HYD-NORTH", "Hydronic North", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(8, 6, 0), new Vertex(0, 6, 0), new Vertex(0, 6, 3), new Vertex(8, 6, 3),
            }),
            Surface("HYD-WEST", "Hydronic West", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(0, 6, 0), new Vertex(0, 0, 0), new Vertex(0, 0, 3), new Vertex(0, 6, 3),
            }),
            Surface("HYD-EAST", "Hydronic East", SurfaceType.Wall, SurfaceBoundary.Outdoors, construction, new[]
            {
                new Vertex(8, 0, 0), new Vertex(8, 6, 0), new Vertex(8, 6, 3), new Vertex(8, 0, 3),
            }),
        };
        Schedule heating = Schedule.Constant("Hydronic Heating", 20, ScheduleType.Temperature);
        Schedule cooling = Schedule.Constant("Hydronic Cooling", 27, ScheduleType.Temperature);
        Schedule availability = Schedule.Constant("Hydronic HVAC Availability", 1, ScheduleType.OnOff);
        var profile = new ZoneProfile(
            new EntityId("HYD-PROFILE"),
            "Hydronic Profile",
            heatingSetpoint: heating,
            coolingSetpoint: cooling,
            hvacAvailability: availability);
        return new Zone(
            new EntityId("HYD-ZONE"),
            "Hydronic Zone",
            surfaces,
            profile,
            infiltrationAirChangesPerHour: 0.3);
    }

    private static EnergyModel ModelWith(Zone zone, SupplySystem system, string name)
    {
        return new EnergyModel(
            name,
            new[] { zone },
            new[]
            {
                new ZoneHvacAssignment(
                    zone.Id,
                    new SupplyGroup(new[] { system })),
            });
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

    public sealed class HydronicEnergyPlusFactAttribute : FactAttribute
    {
        public HydronicEnergyPlusFactAttribute()
        {
            if (!string.Equals(
                Environment.GetEnvironmentVariable("DRAGONS_RUN_ENERGYPLUS_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
            {
                Skip = "Set DRAGONS_RUN_ENERGYPLUS_INTEGRATION=1 to run hydronic HVAC in EnergyPlus 24.2.";
            }
        }
    }
}
