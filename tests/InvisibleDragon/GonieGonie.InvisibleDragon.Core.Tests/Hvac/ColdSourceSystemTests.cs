using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Tests.Hvac;

public sealed class ColdSourceSystemTests
{
    public static TheoryData<CompressorType, string, double> CompressorCases => new()
    {
        { CompressorType.Turbo, "Curve:Quadratic", 0.257183345 },
        { CompressorType.Screw, "Curve:Cubic", 0.907133913 },
        { CompressorType.Reciprocating, "Curve:Quadratic", 0.9441897 },
    };

    public static TheoryData<CoolingTower, string> CoolingTowerCases => new()
    {
        {
            new OpenSingleSpeedCoolingTower(new EntityId("CT-OPEN-ONE"), "Open one"),
            "CoolingTower:SingleSpeed"
        },
        {
            new OpenTwoSpeedCoolingTower(new EntityId("CT-OPEN-TWO"), "Open two"),
            "CoolingTower:TwoSpeed"
        },
        {
            new ClosedSingleSpeedCoolingTower(new EntityId("CT-CLOSED-ONE"), "Closed one"),
            "FluidCooler:SingleSpeed"
        },
        {
            new ClosedTwoSpeedCoolingTower(new EntityId("CT-CLOSED-TWO"), "Closed two"),
            "FluidCooler:TwoSpeed"
        },
    };

    [Theory]
    [MemberData(nameof(CompressorCases))]
    public void ChillerExportsPinnedCompressorCurveFamily(
        CompressorType compressor,
        string partLoadCurveType,
        double firstCapacityCoefficient)
    {
        Chiller chiller = CreateChiller(compressor, OpenTower("CT-CURVE"));

        IReadOnlyList<IdfObject> objects = chiller.ToIdfObjects(new IdfGenerationContext());

        Assert.Equal(2, objects.Count(item => item.ObjectType == "Curve:Biquadratic"));
        IdfObject partLoad = Assert.Single(
            objects,
            item => item.ObjectType == partLoadCurveType && item.Name!.EndsWith(":CoolingCOPPLR", StringComparison.Ordinal));
        Assert.Equal($"Curve_for_{chiller.IdfObjectName}:CoolingCOPPLR", partLoad.Name);
        IdfObject capacity = Assert.Single(
            objects,
            item => item.ObjectType == "Curve:Biquadratic" && item.Name!.EndsWith(":CoolingCapaTemp", StringComparison.Ordinal));
        Assert.Equal(firstCapacityCoefficient.ToString("R", System.Globalization.CultureInfo.InvariantCulture), capacity[1]);
    }

    [Theory]
    [MemberData(nameof(CoolingTowerCases))]
    public void EveryCoolingTowerVariantExportsCompleteCondenserLoop(
        CoolingTower tower,
        string expectedObjectType)
    {
        Chiller chiller = CreateChiller(CompressorType.Turbo, tower);

        IReadOnlyList<IdfObject> objects = tower.ToIdfObjects(new IdfGenerationContext(), chiller);

        Assert.Single(objects, item => item.ObjectType == expectedObjectType);
        Assert.Single(objects, item => item.ObjectType == "CondenserLoop");
        Assert.Single(objects, item => item.ObjectType == "CondenserEquipmentList");
        Assert.Single(objects, item => item.ObjectType == "CondenserEquipmentOperationSchemes");
        Assert.Single(objects, item => item.ObjectType == "SetpointManager:FollowOutdoorAirTemperature");
        Assert.Equal(5, objects.Count(item => item.ObjectType == "Pipe:Adiabatic"));
        Assert.Equal(8, objects.Count(item => item.ObjectType == "Branch"));
        Assert.Contains(
            objects,
            item => item.ObjectType == "Branch"
                && item.Name == $"{CoolingTower.LoopNameFor(chiller)} Demand MainChiller"
                && item[2] == chiller.IdfObjectType
                && item[3] == chiller.IdfObjectName);
    }

    [Fact]
    public void ChillerExportsExtensibleCoolingDemandTopologyDeterministically()
    {
        Chiller chiller = CreateChiller(CompressorType.Screw, OpenTower("CT-DEMAND"));
        var demand = new PlantDemandConnection(
            "Fan coil cooling demand",
            "Coil:Cooling:Water",
            "Fan coil cooling coil",
            "Cooling coil inlet",
            "Cooling coil outlet");

        IReadOnlyList<IdfObject> first = chiller.ToIdfObjects(
            new IdfGenerationContext(),
            new[] { demand });
        IReadOnlyList<IdfObject> second = chiller.ToIdfObjects(
            new IdfGenerationContext(),
            new[] { demand });

        Assert.Single(first, item => item.ObjectType == "Chiller:Electric:EIR");
        Assert.Single(first, item => item.ObjectType == "PlantLoop");
        Assert.Single(first, item => item.ObjectType == "CondenserLoop");
        Assert.Contains(first, item => item.ObjectType == "Branch" && item.Name == demand.BranchName);
        Assert.Equal(
            Serialize(first),
            Serialize(second));
    }

    [Fact]
    public void AbsorptionChillerExportsCondenserAndHotWaterGeneratorLoops()
    {
        var boiler = new Boiler(
            new EntityId("ABS-BOILER"),
            "Absorption generator",
            Fuel.Propane,
            nominalThermalEfficiency: 0.84,
            nominalCapacityWatts: 180_000);
        var tower = new ClosedTwoSpeedCoolingTower(
            new EntityId("ABS-TOWER"),
            "Absorption tower",
            nominalCapacityWatts: 150_000);
        var chiller = new AbsorptionChiller(
            new EntityId("ABS-CHILLER"),
            "Absorption",
            0.72,
            boiler,
            tower,
            nominalCapacityWatts: 120_000);

        IReadOnlyList<IdfObject> objects = chiller.ToIdfObjects(new IdfGenerationContext());

        Assert.Equal(Fuel.Propane, chiller.GeneratorFuel);
        Assert.Single(objects, item => item.ObjectType == "Chiller:Absorption");
        Assert.Single(objects, item => item.ObjectType == "Boiler:HotWater");
        Assert.Equal(2, objects.Count(item => item.ObjectType == "PlantLoop"));
        Assert.Single(objects, item => item.ObjectType == "CondenserLoop");
        IdfObject generator = Assert.Single(
            objects,
            item => item.ObjectType == "Branch"
                && item.Name == $"{boiler.LoopName} Demand MainGenerator_for_{chiller.IdfObjectName}");
        Assert.Equal("Chiller:Absorption", generator[2]);
        Assert.Equal(chiller.IdfObjectName, generator[3]);
        IdfObject absorption = Assert.Single(objects, item => item.ObjectType == "Chiller:Absorption");
        Assert.Equal((0.03303 / 0.72).ToString("R", System.Globalization.CultureInfo.InvariantCulture), absorption[13]);
        Assert.Equal("HotWater", absorption[23]);
    }

    [Fact]
    public void ColdSourcesRejectInvalidOptionsAndIdentifierCollisions()
    {
        var shared = new EntityId("SHARED-COLD-ID");
        var tower = new OpenSingleSpeedCoolingTower(shared, "Tower");

        Assert.Throws<ArgumentOutOfRangeException>(() => new Chiller(
            new EntityId("BAD-COP"),
            "Bad COP",
            0,
            CompressorType.Turbo,
            OpenTower("BAD-COP-TOWER")));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClosedSingleSpeedCoolingTower(
            new EntityId("BAD-PUMP"),
            "Bad pump",
            pumpMotorEfficiency: 1.1));
        Assert.Throws<ArgumentException>(() => new Chiller(
            shared,
            "Collision",
            3,
            CompressorType.Turbo,
            tower));
    }

    [Fact]
    public void AllColdSourceFamiliesPassInstalledEnergyPlus242IddSemantics()
    {
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("GONIEGONIE_ENERGYPLUS_ROOT")
                ?? Environment.GetEnvironmentVariable("ENERGYPLUS_ROOT")
                ?? @"C:\EnergyPlusV24-2-0",
            "Energy+.idd");
        if (!File.Exists(path))
        {
            return;
        }

        IddSchema schema = IddParser.ParseFile(path);
        IdfDocument document = Model.EnergyModelFixtureMatrixTests
            .CreateRepresentativeModel()
            .ToIdfDocument(schema);
        Chiller[] chillers =
        {
            CreateChiller(
                CompressorType.Turbo,
                new OpenSingleSpeedCoolingTower(new EntityId("IDD-CT-1"), "IDD tower 1"),
                "IDD-CHILLER-1"),
            CreateChiller(
                CompressorType.Screw,
                new OpenTwoSpeedCoolingTower(new EntityId("IDD-CT-2"), "IDD tower 2"),
                "IDD-CHILLER-2"),
            CreateChiller(
                CompressorType.Reciprocating,
                new ClosedSingleSpeedCoolingTower(new EntityId("IDD-CT-3"), "IDD tower 3"),
                "IDD-CHILLER-3"),
            CreateChiller(
                CompressorType.Turbo,
                new ClosedTwoSpeedCoolingTower(new EntityId("IDD-CT-4"), "IDD tower 4"),
                "IDD-CHILLER-4"),
        };
        foreach (Chiller chiller in chillers)
        {
            Append(document, chiller.ToIdfObjects(new IdfGenerationContext(schema)));
        }

        var boiler = new Boiler(new EntityId("IDD-ABS-BOILER"), "IDD absorption boiler", Fuel.NaturalGas);
        var absorption = new AbsorptionChiller(
            new EntityId("IDD-ABS"),
            "IDD absorption",
            0.7,
            boiler,
            new OpenSingleSpeedCoolingTower(new EntityId("IDD-ABS-CT"), "IDD absorption tower"));
        Append(document, absorption.ToIdfObjects(new IdfGenerationContext(schema)));
        document.ApplyDefaults();

        ValidationResult result = IdfValidator.Validate(
            document,
            new IdfValidationOptions
            {
                ValidateSchemaDefaults = false,
            });

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
    }

    private static Chiller CreateChiller(
        CompressorType compressor,
        CoolingTower tower,
        string id = "CHILLER-TEST") => new(
            new EntityId(id),
            id,
            3.2,
            compressor,
            tower,
            nominalCapacityWatts: 100_000);

    private static OpenSingleSpeedCoolingTower OpenTower(string id) => new(
        new EntityId(id),
        id,
        nominalCapacityWatts: 125_000);

    private static string Serialize(IEnumerable<IdfObject> objects)
    {
        var document = new IdfDocument(objects: objects);
        return IdfWriter.Write(document);
    }

    private static void Append(IdfDocument document, IEnumerable<IdfObject> objects)
    {
        foreach (IdfObject item in objects)
        {
            document.Append(item);
        }
    }
}
