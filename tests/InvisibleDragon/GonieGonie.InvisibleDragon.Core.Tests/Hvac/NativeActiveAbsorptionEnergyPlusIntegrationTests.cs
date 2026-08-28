using System.Security.Cryptography;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Results;
using GonieGonie.InvisibleDragon.Shape;
using GonieGonie.InvisibleDragon.Tests.Model;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Tests.Hvac;

public sealed class NativeActiveAbsorptionEnergyPlusIntegrationTests
{
    private const string ReportName = "NativeActiveAbsorptionMonthly";
    private const string WeatherRelativePath =
        @"WeatherData\USA_IL_Chicago-OHare.Intl.AP.725300_TMY3.epw";
    private const string WeatherSha256 =
        "c7d4efcf93ba316a1d874352e743df5cf137ba5c0e3459eb2dc4b5442d5b7f5c";

    private static readonly string[] EnergyMetrics =
    {
        "Zone Air System Sensible Cooling Energy",
        "Cooling Coil Total Cooling Energy",
        "Chiller Evaporator Cooling Energy",
        "Chiller Source Hot Water Energy",
        "Chiller Condenser Heat Transfer Energy",
        "Boiler Heating Energy",
        "Boiler NaturalGas Energy",
        "Heating:NaturalGas",
        "NaturalGas:Plant",
    };

    [EnergyPlusRunnableModelTests.EnergyPlusGeneratedModelFact]
    public async Task NativeFanCoilAbsorptionChillerProducesCoolingAndGeneratorEnergyInEnergyPlus242()
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
        string weather = Path.Combine(runtimeRoot, WeatherRelativePath);
        Assert.True(File.Exists(weather), $"Pinned Chicago EPW was not found: {weather}");
        Assert.Equal(WeatherSha256, Sha256(weather));

        string inputDirectory = Path.Combine(
            repository,
            "temp",
            "integration",
            "native-active-absorption",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inputDirectory);
        string input = Path.Combine(inputDirectory, "native-active-absorption.idf");
        try
        {
            IddSchema schema = IddParser.ParseFile(resolution.Runtime!.IddPath);
            (EnergyModel model, AbsorptionChiller chiller, Boiler boiler) = CreateActiveModel();
            var options = new EnergyModelIdfOptions
            {
                AddIdealLoadsForUnassignedZones = false,
                UseLegacySimpleDragonHvacTopology = false,
            };
            IdfDocument document = model.ToIdfDocument(schema, options);
            ConfigureOneDayRunPeriod(document);
            AppendEnergyReport(document, schema, options);
            AssertNativeTopology(document, chiller, boiler);

            ValidationResult validation = IdfValidator.Validate(
                document,
                new IdfValidationOptions { ValidateSchemaDefaults = false });
            Assert.True(
                validation.IsValid,
                string.Join(
                    Environment.NewLine,
                    validation.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
            IdfWriter.WriteFile(input, document);

            EnergyPlusRunResult result = await new EnergyPlusRunner().RunAsync(
                new EnergyPlusRunRequest(
                    resolution.Runtime,
                    input,
                    weather,
                    Path.Combine(repository, "temp", "integration", "native-active-absorption-runs"))
                {
                    Timeout = TimeSpan.FromMinutes(3),
                    CleanupPolicy = EnergyPlusCleanupPolicy.DeleteOnSuccess,
                });

            Assert.True(result.IsSuccess, FormatFailure(result));
            Assert.False(result.WorkDirectoryRetained);
            Assert.Null(result.CleanupError);
            Assert.Equal(0, result.Outputs.ErrorSummary.FatalCount);
            Assert.Equal(0, result.Outputs.ErrorSummary.SevereCount);

            EnergyPlusSimulationResult parsed = EnergyPlusResultParser.Parse(result);
            Assert.True(parsed.ErrorLog.Summary.CompletedSuccessfully);
            Assert.Equal("24.2.0", parsed.ErrorLog.EnergyPlusVersion);
            Assert.Equal("94a887817b", parsed.ErrorLog.EnergyPlusBuild);

            string errorLog = Assert.IsType<string>(result.Outputs.Error?.TextContent);
            Assert.DoesNotContain("Plant temperatures are getting far too hot", errorLog, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("run away plant temperatures", errorLog, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Plant loop exceeding upper temperature", errorLog, StringComparison.OrdinalIgnoreCase);

            IReadOnlyDictionary<string, double> energy = ReadAugustEnergy(parsed, chiller, boiler);
            foreach ((string metric, double value) in energy)
            {
                Assert.True(double.IsFinite(value), $"{metric} was not finite: {value}");
                Assert.True(value > 1d, $"{metric} did not prove active operation: {value} kWh");
            }

            double evaporator = energy["Chiller Evaporator Cooling Energy"];
            double generator = energy["Chiller Source Hot Water Energy"];
            double condenser = energy["Chiller Condenser Heat Transfer Energy"];
            double boilerHeating = energy["Boiler Heating Energy"];
            double boilerGas = energy["Boiler NaturalGas Energy"];
            Assert.True(condenser > evaporator);
            Assert.True(condenser > generator);
            Assert.InRange(evaporator / generator, 0.80d, 0.83d);
            AssertRelativeEnergyBalance(evaporator + generator, condenser, 0.03d);
            Assert.InRange(Math.Abs((boilerHeating / boilerGas) - 0.88d), 0d, 0.02d);
            AssertWithinTolerance(boilerGas, energy["Heating:NaturalGas"]);
            AssertWithinTolerance(boilerGas, energy["NaturalGas:Plant"]);
        }
        finally
        {
            if (Directory.Exists(inputDirectory))
            {
                Directory.Delete(inputDirectory, recursive: true);
            }
        }
    }

    private static (EnergyModel Model, AbsorptionChiller Chiller, Boiler Boiler) CreateActiveModel()
    {
        var material = new Material("Native absorption concrete", 1.4, 2200, 880);
        var construction = new OpaqueConstruction(
            "Native absorption envelope",
            new[] { new Layer("Native absorption concrete layer", material, 0.2) });
        Surface[] surfaces =
        {
            Surface("NAA-FLOOR", "Native absorption floor", SurfaceType.Floor, construction, new[]
            {
                new Vertex(0, 0, 0), new Vertex(0, 6, 0), new Vertex(8, 6, 0), new Vertex(8, 0, 0),
            }),
            Surface("NAA-ROOF", "Native absorption roof", SurfaceType.Ceiling, construction, new[]
            {
                new Vertex(0, 0, 3), new Vertex(8, 0, 3), new Vertex(8, 6, 3), new Vertex(0, 6, 3),
            }),
            Surface("NAA-SOUTH", "Native absorption south", SurfaceType.Wall, construction, new[]
            {
                new Vertex(0, 0, 0), new Vertex(8, 0, 0), new Vertex(8, 0, 3), new Vertex(0, 0, 3),
            }),
            Surface("NAA-NORTH", "Native absorption north", SurfaceType.Wall, construction, new[]
            {
                new Vertex(8, 6, 0), new Vertex(0, 6, 0), new Vertex(0, 6, 3), new Vertex(8, 6, 3),
            }),
            Surface("NAA-WEST", "Native absorption west", SurfaceType.Wall, construction, new[]
            {
                new Vertex(0, 6, 0), new Vertex(0, 0, 0), new Vertex(0, 0, 3), new Vertex(0, 6, 3),
            }),
            Surface("NAA-EAST", "Native absorption east", SurfaceType.Wall, construction, new[]
            {
                new Vertex(8, 0, 0), new Vertex(8, 6, 0), new Vertex(8, 6, 3), new Vertex(8, 0, 3),
            }),
        };

        Schedule cooling = Schedule.Constant("Native absorption cooling", 20, ScheduleType.Temperature);
        Schedule availability = Schedule.Constant("Native absorption availability", 1, ScheduleType.OnOff);
        Schedule equipment = Schedule.Constant("Native absorption equipment", 100, ScheduleType.Real);
        var profile = new ZoneProfile(
            new EntityId("NAA-PROFILE"),
            "Native absorption profile",
            coolingSetpoint: cooling,
            hvacAvailability: availability,
            equipment: equipment);
        var zone = new Zone(
            new EntityId("NAA-ZONE"),
            "Native absorption zone",
            surfaces,
            profile,
            infiltrationAirChangesPerHour: 0);

        var boiler = new Boiler(
            new EntityId("NAA-BOILER"),
            "Native absorption generator",
            Fuel.NaturalGas,
            nominalThermalEfficiency: 0.88,
            nominalCapacityWatts: 25_000,
            setpointTemperatureCelsius: 80);
        var tower = new OpenSingleSpeedCoolingTower(
            new EntityId("NAA-TOWER"),
            "Native absorption tower",
            nominalCapacityWatts: 30_000);
        var chiller = new AbsorptionChiller(
            new EntityId("NAA-CHILLER"),
            "Native active absorption chiller",
            0.72,
            boiler,
            tower,
            nominalCapacityWatts: 12_000,
            setpointTemperatureCelsius: 6);
        var fanCoil = new FanCoilUnit(
            new EntityId("NAA-FCU"),
            "Native active absorption fan coil",
            chiller);
        var model = new EnergyModel(
            "Native active absorption model",
            new[] { zone },
            new[] { new ZoneHvacAssignment(zone.Id, new SupplyGroup(new[] { fanCoil })) });
        return (model, chiller, boiler);
    }

    private static Surface Surface(
        string id,
        string name,
        SurfaceType type,
        OpaqueConstruction construction,
        IEnumerable<Vertex> vertices) => new(
            new EntityId(id),
            name,
            type,
            construction,
            SurfaceBoundary.Adiabatic,
            new PlanarPolygon(vertices));

    private static void ConfigureOneDayRunPeriod(IdfDocument document)
    {
        IdfObject runPeriod = Assert.Single(document["RunPeriod"]);
        runPeriod["Name"] = "Native active absorption day";
        runPeriod["Begin Month"] = "8";
        runPeriod["Begin Day of Month"] = "1";
        runPeriod["Begin Year"] = "2026";
        runPeriod["End Month"] = "8";
        runPeriod["End Day of Month"] = "1";
        runPeriod["End Year"] = "2026";
    }

    private static void AppendEnergyReport(
        IdfDocument document,
        IddSchema schema,
        EnergyModelIdfOptions options)
    {
        var values = new List<object?> { ReportName, 6 };
        foreach (string metric in EnergyMetrics)
        {
            values.Add(metric);
            values.Add("SumOrAverage");
        }

        document.Append(new IdfGenerationContext(schema, options).CreateRaw(
            "Output:Table:Monthly",
            values.ToArray()));
    }

    private static void AssertNativeTopology(
        IdfDocument document,
        AbsorptionChiller chiller,
        Boiler boiler)
    {
        IdfObject tableStyle = Assert.Single(document["OutputControl:Table:Style"]);
        Assert.Equal("Comma", tableStyle[0]);
        Assert.Equal("JtoKWH", tableStyle[1]);
        Assert.Single(document["Chiller:Absorption"]);
        Assert.Single(document["Boiler:HotWater"]);
        Assert.Single(document["CoolingTower:SingleSpeed"]);
        Assert.Single(document["Coil:Cooling:Water"]);
        Assert.Single(document["ZoneHVAC:FourPipeFanCoil"]);
        Assert.Single(document["CondenserLoop"]);
        IdfObject generatorBranch = Assert.Single(document["Branch"], item =>
            item.Name == $"{boiler.LoopName} Demand MainGenerator_for_{chiller.IdfObjectName}");
        Assert.Equal(chiller.IdfObjectType, generatorBranch[2]);
        Assert.Equal(chiller.IdfObjectName, generatorBranch[3]);
        Assert.Equal($"{chiller.IdfObjectName} Generator InletNode", generatorBranch[4]);
        Assert.Equal($"{chiller.IdfObjectName} Generator OutletNode", generatorBranch[5]);
        Assert.DoesNotContain(document["Branch"], item =>
            item.Name == $"{boiler.LoopName} Demand MainGenerator");
    }

    private static Dictionary<string, double> ReadAugustEnergy(
        EnergyPlusSimulationResult result,
        AbsorptionChiller chiller,
        Boiler boiler)
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (string metric in EnergyMetrics)
        {
            string expectedScope = metric switch
            {
                "Zone Air System Sensible Cooling Energy" => "Native absorption zone",
                "Cooling Coil Total Cooling Energy" => "CoolingCoil_for_",
                "Chiller Evaporator Cooling Energy" or
                "Chiller Source Hot Water Energy" or
                "Chiller Condenser Heat Transfer Energy" => chiller.IdfObjectName,
                "Boiler Heating Energy" or "Boiler NaturalGas Energy" => boiler.IdfObjectName,
                "Heating:NaturalGas" or "NaturalGas:Plant" => "Meter",
                _ => throw new InvalidOperationException($"No expected scope is defined for {metric}."),
            };
            EnergyPlusTabularTable table = Assert.Single(
                result.Tables,
                item => string.Equals(item.ReportName, ReportName, StringComparison.OrdinalIgnoreCase)
                    && (metric == "Cooling Coil Total Cooling Energy"
                        ? item.Scope.StartsWith(expectedScope, StringComparison.OrdinalIgnoreCase)
                        : string.Equals(item.Scope, expectedScope, StringComparison.OrdinalIgnoreCase))
                    && item.Header.Cells.Any(cell =>
                        cell.Text.StartsWith(metric, StringComparison.OrdinalIgnoreCase))
                    && item.FindRows("August").Count() == 1);
            EnergyPlusTabularRow august = Assert.Single(table.FindRows("August"));
            int[] matchingColumns = table.Header.Cells
                .Select((cell, index) => (cell.Text, index))
                .Where(item => item.Text.StartsWith(metric, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .ToArray();
            int column = Assert.Single(matchingColumns);
            Assert.EndsWith("[kWh]", table.Header[column].Text, StringComparison.OrdinalIgnoreCase);
            Assert.True(column < august.Cells.Count, $"{metric} column was absent from the August row.");
            double value = Assert.IsType<double>(august[column].NumericValue);
            values.Add(metric, value);
        }

        return values;
    }

    private static void AssertWithinTolerance(double expected, double actual)
    {
        double tolerance = 0.01d + (0.001d * Math.Max(Math.Abs(expected), Math.Abs(actual)));
        Assert.InRange(Math.Abs(expected - actual), 0d, tolerance);
    }

    private static void AssertRelativeEnergyBalance(double expected, double actual, double relativeTolerance)
    {
        double scale = Math.Max(Math.Abs(expected), Math.Abs(actual));
        Assert.True(scale > 0d);
        Assert.InRange(Math.Abs(expected - actual) / scale, 0d, relativeTolerance);
    }

    private static string Sha256(string path) => Convert.ToHexString(
        SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

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
}
