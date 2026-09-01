using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;

namespace Dragons.InvisibleDragon.Tests.Hvac;

public sealed class ColdSourceSystemTests
{
    public static TheoryData<CompressorType, string, string, double> CompressorCases => new()
    {
        { CompressorType.Turbo, "Chiller:Electric:EIR", "Curve:Quadratic", 0.257183345 },
        { CompressorType.Screw, "Chiller:Electric:ReformulatedEIR", "Curve:Bicubic", 0.907133913 },
        { CompressorType.Reciprocating, "Chiller:Electric:EIR", "Curve:Quadratic", 0.9441897 },
    };

    public static TheoryData<double, double, double> ScrewBicubicSweep => new()
    {
        { 14.56, 0.18, 0.32177603708051999 },
        { 14.56, 0.50, 0.41725117374292003 },
        { 14.56, 1.03, 1.0253107217639950 },
        { 20.00, 0.18, 0.43511540492244005 },
        { 20.00, 0.50, 0.48318277412499999 },
        { 20.00, 1.03, 1.0127232072907151 },
        { 29.00, 0.18, 0.63068124499643996 },
        { 29.00, 0.50, 0.60031664597500012 },
        { 29.00, 1.03, 0.99995413176971526 },
        { 34.97, 0.18, 0.76594643743133994 },
        { 34.97, 0.50, 0.68355529948797999 },
        { 34.97, 1.03, 0.99702383019326490 },
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
        string chillerObjectType,
        string partLoadCurveType,
        double firstCapacityCoefficient)
    {
        Chiller chiller = CreateChiller(compressor, OpenTower("CT-CURVE"));

        IReadOnlyList<IdfObject> objects = chiller.ToIdfObjects(new IdfGenerationContext());

        Assert.Equal(chillerObjectType, chiller.IdfObjectType);
        Assert.Single(objects, item => item.ObjectType == chillerObjectType);
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

    [Fact]
    public void ScrewChillerExportsPinnedBicubicFieldsAndReformulatedEirConnection()
    {
        Chiller chiller = CreateChiller(CompressorType.Screw, OpenTower("CT-SCREW-BICUBIC"));

        IReadOnlyList<IdfObject> objects = chiller.ToIdfObjects(new IdfGenerationContext());

        IdfObject curve = Assert.Single(
            objects,
            item => item.ObjectType == "Curve:Bicubic"
                && item.Name!.EndsWith(":CoolingCOPPLR", StringComparison.Ordinal));
        double[] expectedFields =
        {
            0.044612112,
            0.023594163,
            0.0000619872,
            -0.353684198,
            1.797965254,
            -0.0272333223,
            0,
            -0.467387755,
            0,
            0,
            14.56,
            34.97,
            0.18,
            1.03,
        };
        double[] actualFields = Enumerable.Range(1, expectedFields.Length)
            .Select(index => double.Parse(
                curve[index],
                System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        Assert.Equal(expectedFields, actualFields);

        IdfObject component = Assert.Single(
            objects,
            item => item.ObjectType == "Chiller:Electric:ReformulatedEIR");
        Assert.Equal("LeavingCondenserWaterTemperature", component[9]);
        Assert.Equal(curve.Name, component[10]);
        Assert.Contains(
            objects,
            item => item.ObjectType == "Branch"
                && item.Name == $"{chiller.LoopName} Supply MainComponent"
                && item[2] == chiller.IdfObjectType);
        Assert.Contains(
            objects,
            item => item.ObjectType == "Branch"
                && item.Name == $"{CoolingTower.LoopNameFor(chiller)} Demand MainChiller"
                && item[2] == chiller.IdfObjectType);
        Assert.Contains(
            objects,
            item => item.ObjectType == "PlantEquipmentList"
                && item[1] == chiller.IdfObjectType);
        Assert.DoesNotContain(
            objects,
            item => item.ObjectType == "Curve:Cubic"
                && item.Name!.EndsWith(":CoolingCOPPLR", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacySimpleDragonScrewChillerUsesElectricEirAcrossPlantReferences()
    {
        var tower = new ClosedTwoSpeedCoolingTower(
            new EntityId("CT-SCREW-LEGACY"),
            "Legacy screw tower",
            nominalCapacityWatts: 125_000);
        var chiller = new Chiller(
            new EntityId("CHILLER-SCREW-LEGACY"),
            "Legacy screw",
            5.2,
            CompressorType.Screw,
            tower,
            nominalCapacityWatts: 100_000,
            setpointTemperatureCelsius: 7.25);

        IReadOnlyList<IdfObject> native = chiller.ToIdfObjects(new IdfGenerationContext());
        IReadOnlyList<IdfObject> legacy = chiller.ToIdfObjects(LegacyContext());

        Assert.Equal("Chiller:Electric:ReformulatedEIR", chiller.IdfObjectType);
        IdfObject nativeCurve = ObjectNamed(
            native,
            "Curve:Bicubic",
            $"Curve_for_{chiller.IdfObjectName}:CoolingCOPPLR");
        IdfObject nativeComponent = ObjectNamed(
            native,
            "Chiller:Electric:ReformulatedEIR",
            chiller.IdfObjectName);
        Assert.Equal("7.25", nativeComponent[3]);
        Assert.Equal("29", nativeComponent[4]);
        Assert.Equal("LeavingCondenserWaterTemperature", nativeComponent[9]);
        Assert.Equal(nativeCurve.Name, nativeComponent[10]);
        Assert.DoesNotContain(native, item => item.ObjectType == "Chiller:Electric:EIR");
        AssertChillerPlantReferenceType(native, chiller, "Chiller:Electric:ReformulatedEIR");

        IdfObject legacyCurve = ObjectNamed(
            legacy,
            "Curve:Bicubic",
            $"Curve_for_{chiller.IdfObjectName}:CoolingCOPPLR");
        IdfObject legacyComponent = ObjectNamed(
            legacy,
            "Chiller:Electric:EIR",
            chiller.IdfObjectName);
        Assert.Equal(nativeCurve.Name, legacyCurve.Name);
        Assert.Equal("6.67", legacyComponent[3]);
        Assert.Equal("29.4", legacyComponent[4]);
        Assert.Equal(legacyCurve.Name, legacyComponent[9]);
        Assert.Equal("WaterCooled", legacyComponent[18]);
        Assert.Equal("1", legacyComponent[20]);
        Assert.Equal("2", legacyComponent[21]);
        Assert.Equal("NotModulated", legacyComponent[22]);
        Assert.DoesNotContain(
            legacy,
            item => item.ObjectType == "Chiller:Electric:ReformulatedEIR");
        AssertChillerPlantReferenceType(legacy, chiller, "Chiller:Electric:EIR");

        IdfObject nativeSetpoint = ObjectNamed(
            native,
            "Schedule:Constant",
            $"{chiller.LoopName} SetpointTemperature");
        IdfObject legacySetpoint = ObjectNamed(
            legacy,
            "Schedule:Constant",
            $"{chiller.LoopName} SetpointTemperature");
        IdfObject nativeSizing = ObjectNamed(native, "Sizing:Plant", chiller.LoopName);
        IdfObject legacySizing = ObjectNamed(legacy, "Sizing:Plant", chiller.LoopName);
        Assert.Equal("7.25", nativeSetpoint[2]);
        Assert.Equal(nativeSetpoint[2], legacySetpoint[2]);
        Assert.Equal("7.25", nativeSizing[2]);
        Assert.Equal("6.0", legacySizing[2]);
    }

    [Fact]
    public void ClosedTwoSpeedCoolingTowerSwitchesOnlyLegacyPerformanceFields()
    {
        var tower = new ClosedTwoSpeedCoolingTower(
            new EntityId("CT-CLOSED-TWO-MODES"),
            "Closed two modes",
            nominalCapacityWatts: 125_000);
        Chiller chiller = CreateChiller(
            CompressorType.Turbo,
            tower,
            "CHILLER-CLOSED-TWO-MODES");

        IdfObject native = ObjectNamed(
            tower.ToIdfObjects(new IdfGenerationContext(), chiller),
            "FluidCooler:TwoSpeed",
            CoolingTower.ObjectNameFor(chiller));
        IdfObject legacy = ObjectNamed(
            tower.ToIdfObjects(LegacyContext(), chiller),
            "FluidCooler:TwoSpeed",
            CoolingTower.ObjectNameFor(chiller));

        Assert.Equal("NominalCapacity", native[3]);
        Assert.Equal(string.Empty, native[4]);
        Assert.Equal(string.Empty, native[5]);
        Assert.Equal("125000.0", native[7]);
        Assert.Equal("autocalculate", native[8]);

        Assert.Equal("UFactorTimesAreaAndDesignWaterFlowRate", legacy[3]);
        Assert.Equal("autosize", legacy[4]);
        Assert.Equal("autocalculate", legacy[5]);
        Assert.Equal(string.Empty, legacy[7]);
        Assert.Equal(string.Empty, legacy[8]);

        string[] unchangedFields =
        {
            "35",
            "28",
            "25.56",
            "autosize",
            "autosize",
            "autosize",
            "autocalculate",
            "0.5",
            "autocalculate",
            "0.16",
        };
        for (int offset = 0; offset < unchangedFields.Length; offset++)
        {
            int index = 10 + offset;
            Assert.Equal(unchangedFields[offset], native[index]);
            Assert.Equal(native[index], legacy[index]);
        }
    }

    [Fact]
    public void OpenCoolingTowersEmitPinnedLegacySizingFields()
    {
        var single = new OpenSingleSpeedCoolingTower(
            new EntityId("CT-OPEN-SINGLE-MODES"),
            "Open single modes",
            nominalCapacityWatts: 125_000);
        Chiller singleChiller = CreateChiller(
            CompressorType.Turbo,
            single,
            "CHILLER-OPEN-SINGLE-MODES");
        IdfObject nativeSingle = ObjectNamed(
            single.ToIdfObjects(new IdfGenerationContext(), singleChiller),
            "CoolingTower:SingleSpeed",
            CoolingTower.ObjectNameFor(singleChiller));
        IdfObject legacySingle = ObjectNamed(
            single.ToIdfObjects(LegacyContext(), singleChiller),
            "CoolingTower:SingleSpeed",
            CoolingTower.ObjectNameFor(singleChiller));

        Assert.Equal(string.Empty, nativeSingle[3]);
        Assert.Equal(string.Empty, nativeSingle[6]);
        Assert.Equal(string.Empty, nativeSingle[9]);
        Assert.Equal("NominalCapacity", nativeSingle[11]);
        Assert.Equal(38, legacySingle.Count);
        Assert.Equal("autosize", legacySingle[3]);
        Assert.Equal("autosize", legacySingle[6]);
        Assert.Equal("autocalculate", legacySingle[9]);
        Assert.Equal("UFactorTimesAreaAndDesignWaterFlowRate", legacySingle[11]);
        Assert.Equal("125000.0", legacySingle[13]);
        Assert.Equal("FanCycling", legacySingle[31]);
        Assert.Equal("General", legacySingle[37]);

        var twoSpeed = new OpenTwoSpeedCoolingTower(
            new EntityId("CT-OPEN-TWO-MODES"),
            "Open two modes",
            nominalCapacityWatts: 150_000);
        Chiller twoSpeedChiller = CreateChiller(
            CompressorType.Turbo,
            twoSpeed,
            "CHILLER-OPEN-TWO-MODES");
        IdfObject nativeTwoSpeed = ObjectNamed(
            twoSpeed.ToIdfObjects(new IdfGenerationContext(), twoSpeedChiller),
            "CoolingTower:TwoSpeed",
            CoolingTower.ObjectNameFor(twoSpeedChiller));
        IdfObject legacyTwoSpeed = ObjectNamed(
            twoSpeed.ToIdfObjects(LegacyContext(), twoSpeedChiller),
            "CoolingTower:TwoSpeed",
            CoolingTower.ObjectNameFor(twoSpeedChiller));

        Assert.Equal(string.Empty, nativeTwoSpeed[3]);
        Assert.Equal(string.Empty, nativeTwoSpeed[6]);
        Assert.Equal(string.Empty, nativeTwoSpeed[11]);
        Assert.Equal(string.Empty, nativeTwoSpeed[15]);
        Assert.Equal("NominalCapacity", nativeTwoSpeed[17]);
        Assert.Equal(45, legacyTwoSpeed.Count);
        Assert.Equal("autosize", legacyTwoSpeed[3]);
        Assert.Equal("autosize", legacyTwoSpeed[6]);
        Assert.Equal("autocalculate", legacyTwoSpeed[11]);
        Assert.Equal("autocalculate", legacyTwoSpeed[15]);
        Assert.Equal("UFactorTimesAreaAndDesignWaterFlowRate", legacyTwoSpeed[17]);
        Assert.Equal("150000.0", legacyTwoSpeed[19]);
        Assert.Equal("General", legacyTwoSpeed[44]);
    }

    [Fact]
    public void CoolingLoopPumpsSwitchOnlyLegacyMinimumFlowAndControl()
    {
        var tower = new ClosedTwoSpeedCoolingTower(
            new EntityId("CT-PUMP-MODES"),
            "Pump modes tower",
            nominalCapacityWatts: 125_000,
            pumpMotorEfficiency: 0.83);
        var chiller = new Chiller(
            new EntityId("CHILLER-PUMP-MODES"),
            "Pump modes chiller",
            3.2,
            CompressorType.Screw,
            tower,
            nominalCapacityWatts: 100_000,
            pumpMotorEfficiency: 0.87);

        IReadOnlyList<IdfObject> native = chiller.ToIdfObjects(new IdfGenerationContext());
        IReadOnlyList<IdfObject> legacy = chiller.ToIdfObjects(LegacyContext());
        Assert.Equal(2, native.Count(item => item.ObjectType == "Pump:VariableSpeed"));
        Assert.Equal(2, legacy.Count(item => item.ObjectType == "Pump:VariableSpeed"));

        var pumpEfficiencies = new Dictionary<string, string>
        {
            [$"VSDPump_for_{chiller.IdfObjectName}"] = "0.87",
            [$"VSDPump_for_{CoolingTower.ObjectNameFor(chiller)}"] = "0.83",
        };
        foreach ((string pumpName, string expectedEfficiency) in pumpEfficiencies)
        {
            IdfObject nativePump = ObjectNamed(native, "Pump:VariableSpeed", pumpName);
            IdfObject legacyPump = ObjectNamed(legacy, "Pump:VariableSpeed", pumpName);

            Assert.Equal(30, nativePump.Count);
            Assert.Equal(30, legacyPump.Count);
            for (int index = 0; index <= 11; index++)
            {
                Assert.Equal(nativePump[index], legacyPump[index]);
            }

            for (int index = 14; index <= 29; index++)
            {
                Assert.Equal(nativePump[index], legacyPump[index]);
            }

            Assert.Equal(expectedEfficiency, nativePump[6]);
            Assert.Equal("0", nativePump[12]);
            Assert.Equal("Intermittent", nativePump[13]);
            Assert.Equal("autosize", legacyPump[12]);
            Assert.Equal("Continuous", legacyPump[13]);
            Assert.Equal("PowerPerFlowPerPressure", nativePump[25]);
            Assert.Equal("348701.1", nativePump[26]);
            Assert.Equal("1.282051282", nativePump[27]);
            Assert.Equal("0", nativePump[28]);
            Assert.Equal("General", nativePump[29]);
        }
    }

    [Theory]
    [MemberData(nameof(ScrewBicubicSweep))]
    public void ScrewChillerBicubicMatchesPinnedTemperaturePartLoadSurface(
        double leavingCondenserWaterTemperatureCelsius,
        double partLoadRatio,
        double expectedModifier)
    {
        Chiller chiller = CreateChiller(CompressorType.Screw, OpenTower("CT-SCREW-SWEEP"));
        IdfObject curve = Assert.Single(
            chiller.ToIdfObjects(new IdfGenerationContext()),
            item => item.ObjectType == "Curve:Bicubic"
                && item.Name!.EndsWith(":CoolingCOPPLR", StringComparison.Ordinal));

        double actualModifier = EvaluateBicubic(
            curve,
            leavingCondenserWaterTemperatureCelsius,
            partLoadRatio);

        Assert.InRange(Math.Abs(actualModifier - expectedModifier), 0, 1E-12);
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

        Assert.Single(first, item => item.ObjectType == chiller.IdfObjectType);
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
    public void LegacyAbsorptionChillerUsesPinnedUnsuffixedGeneratorBranch()
    {
        var boiler = new Boiler(
            new EntityId("ABS-LEGACY-BOILER"),
            "Legacy absorption generator",
            Fuel.NaturalGas);
        var chiller = new AbsorptionChiller(
            new EntityId("ABS-LEGACY-CHILLER"),
            "Legacy absorption",
            0.9,
            boiler,
            new OpenSingleSpeedCoolingTower(
                new EntityId("ABS-LEGACY-TOWER"),
                "Legacy absorption tower"));

        IReadOnlyList<IdfObject> objects = chiller.ToIdfObjects(LegacyContext());

        IdfObject generator = Assert.Single(
            objects,
            item => item.ObjectType == "Branch"
                && item.Name == $"{boiler.LoopName} Demand MainGenerator");
        Assert.Equal(chiller.IdfObjectType, generator[2]);
        Assert.Equal(chiller.IdfObjectName, generator[3]);
        Assert.DoesNotContain(
            objects,
            item => item.ObjectType == "Branch"
                && item.Name == $"{boiler.LoopName} Demand MainGenerator_for_{chiller.IdfObjectName}");
    }

    [Fact]
    public void LegacyAbsorptionChillerMatchesPinnedSubloopOrderAndFixedSizingSetpoint()
    {
        var boiler = new Boiler(
            new EntityId("ABS-LEGACY-ORDER-BOILER"),
            "Legacy ordered generator",
            Fuel.NaturalGas,
            setpointTemperatureCelsius: 72);
        var tower = new OpenSingleSpeedCoolingTower(
            new EntityId("ABS-LEGACY-ORDER-TOWER"),
            "Legacy ordered tower");
        var chiller = new AbsorptionChiller(
            new EntityId("ABS-LEGACY-ORDER-CHILLER"),
            "Legacy ordered absorption",
            0.9,
            boiler,
            tower,
            setpointTemperatureCelsius: 8.5);

        IReadOnlyList<IdfObject> native = chiller.ToIdfObjects(new IdfGenerationContext());
        IReadOnlyList<IdfObject> first = chiller.ToIdfObjects(LegacyContext());
        IReadOnlyList<IdfObject> second = chiller.ToIdfObjects(LegacyContext());

        Assert.Equal(Serialize(first), Serialize(second));
        Assert.Equal(first.Count, second.Count);
        for (int index = 0; index < first.Count; index++)
        {
            Assert.NotSame(first[index], second[index]);
        }

        Assert.Equal("Legacy ordered absorption", chiller.Name);
        Assert.Equal(8.5, chiller.SetpointTemperatureCelsius);
        Assert.Equal(72, chiller.HeatSource.SetpointTemperatureCelsius);

        IdfObject legacySetpoint = ObjectNamed(
            first,
            "Schedule:Constant",
            $"{chiller.LoopName} SetpointTemperature");
        IdfObject legacySizing = ObjectNamed(first, "Sizing:Plant", chiller.LoopName);
        IdfObject nativeSetpoint = ObjectNamed(
            native,
            "Schedule:Constant",
            $"{chiller.LoopName} SetpointTemperature");
        IdfObject nativeSizing = ObjectNamed(native, "Sizing:Plant", chiller.LoopName);
        Assert.Equal("8.5", legacySetpoint[2]);
        Assert.Equal("6.0", legacySizing[2]);
        Assert.Equal("8.5", nativeSetpoint[2]);
        Assert.Equal("8.5", nativeSizing[2]);

        string generatorBranchName = $"{boiler.LoopName} Demand MainGenerator";
        string towerObjectName = CoolingTower.ObjectNameFor(chiller);
        string towerLoopName = CoolingTower.LoopNameFor(chiller);
        int absorptionComponentIndex = IndexOf(first, "Chiller:Absorption", chiller.IdfObjectName);
        int boilerComponentIndex = IndexOf(first, "Boiler:HotWater", boiler.IdfObjectName);
        int boilerPlantIndex = IndexOf(first, "PlantLoop", boiler.LoopName);
        int boilerSizingIndex = IndexOf(first, "Sizing:Plant", boiler.LoopName);
        int generatorBranchIndex = IndexOf(first, "Branch", generatorBranchName);
        int towerComponentIndex = IndexOf(first, tower.IdfObjectType, towerObjectName);
        int towerLoopIndex = IndexOf(first, "CondenserLoop", towerLoopName);
        int towerSizingIndex = IndexOf(first, "Sizing:Plant", towerLoopName);
        int absorptionPlantIndex = IndexOf(first, "PlantLoop", chiller.LoopName);
        int absorptionSizingIndex = IndexOf(first, "Sizing:Plant", chiller.LoopName);

        Assert.Equal(0, absorptionComponentIndex);
        Assert.True(absorptionComponentIndex < boilerComponentIndex);
        Assert.True(boilerComponentIndex < boilerPlantIndex);
        Assert.Equal(boilerPlantIndex + 1, boilerSizingIndex);
        Assert.Equal(boilerSizingIndex + 1, generatorBranchIndex);
        Assert.Equal(generatorBranchIndex + 1, towerComponentIndex);
        Assert.True(towerComponentIndex < towerLoopIndex);
        Assert.Equal(towerLoopIndex + 1, towerSizingIndex);
        Assert.Equal(towerSizingIndex + 1, absorptionPlantIndex);
        Assert.Equal(absorptionPlantIndex + 1, absorptionSizingIndex);
        Assert.Equal(first.Count - 1, absorptionSizingIndex);

        IdfObject generatorBranch = first[generatorBranchIndex];
        Assert.Equal(chiller.IdfObjectType, generatorBranch[2]);
        Assert.Equal(chiller.IdfObjectName, generatorBranch[3]);
        Assert.Equal($"{chiller.IdfObjectName} Generator InletNode", generatorBranch[4]);
        Assert.Equal($"{chiller.IdfObjectName} Generator OutletNode", generatorBranch[5]);
        foreach (string objectType in new[] { "BranchList", "Connector:Splitter", "Connector:Mixer" })
        {
            IdfObject topology = ObjectNamed(
                first,
                objectType,
                objectType == "BranchList"
                    ? $"{boiler.LoopName} Demand BranchList"
                    : $"{boiler.LoopName} Demand {objectType[(objectType.IndexOf(':') + 1)..]}");
            Assert.Contains(
                generatorBranchName,
                Enumerable.Range(0, topology.Count).Select(index => topology[index]));
        }

        int nativeAbsorptionPlantIndex = IndexOf(native, "PlantLoop", chiller.LoopName);
        int nativeTowerComponentIndex = IndexOf(native, tower.IdfObjectType, towerObjectName);
        int nativeBoilerComponentIndex = IndexOf(native, "Boiler:HotWater", boiler.IdfObjectName);
        int nativeGeneratorBranchIndex = IndexOf(
            native,
            "Branch",
            $"{boiler.LoopName} Demand MainGenerator_for_{chiller.IdfObjectName}");
        int nativeBoilerPlantIndex = IndexOf(native, "PlantLoop", boiler.LoopName);
        Assert.True(nativeAbsorptionPlantIndex < nativeTowerComponentIndex);
        Assert.True(nativeTowerComponentIndex < nativeBoilerComponentIndex);
        Assert.True(nativeBoilerComponentIndex < nativeGeneratorBranchIndex);
        Assert.True(nativeGeneratorBranchIndex < nativeBoilerPlantIndex);
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
            Environment.GetEnvironmentVariable("DRAGONS_ENERGYPLUS_ROOT")
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

    private static IdfGenerationContext LegacyContext() => new(
        options: new EnergyModelIdfOptions
        {
            UseLegacySimpleDragonHvacTopology = true,
        });

    private static IdfObject ObjectNamed(
        IEnumerable<IdfObject> objects,
        string objectType,
        string name) => Assert.Single(
            objects,
            item => item.ObjectType == objectType && item.Name == name);

    private static int IndexOf(
        IReadOnlyList<IdfObject> objects,
        string objectType,
        string name) => objects
            .Select((item, index) => (Item: item, Index: index))
            .Single(pair => pair.Item.ObjectType == objectType && pair.Item.Name == name)
            .Index;

    private static void AssertChillerPlantReferenceType(
        IEnumerable<IdfObject> objects,
        Chiller chiller,
        string expectedObjectType)
    {
        IdfObject supplyBranch = ObjectNamed(
            objects,
            "Branch",
            $"{chiller.LoopName} Supply MainComponent");
        IdfObject condenserDemandBranch = ObjectNamed(
            objects,
            "Branch",
            $"{CoolingTower.LoopNameFor(chiller)} Demand MainChiller");
        IdfObject equipmentList = ObjectNamed(
            objects,
            "PlantEquipmentList",
            $"{chiller.LoopName} EquipmentList");

        Assert.Equal(expectedObjectType, supplyBranch[2]);
        Assert.Equal(expectedObjectType, condenserDemandBranch[2]);
        Assert.Equal(expectedObjectType, equipmentList[1]);
    }

    private static string Serialize(IEnumerable<IdfObject> objects)
    {
        var document = new IdfDocument(objects: objects);
        return IdfWriter.Write(document);
    }

    private static double EvaluateBicubic(IdfObject curve, double x, double y)
    {
        double[] coefficient = Enumerable.Range(1, 10)
            .Select(index => double.Parse(
                curve[index],
                System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        return coefficient[0]
            + (coefficient[1] * x)
            + (coefficient[2] * x * x)
            + (coefficient[3] * y)
            + (coefficient[4] * y * y)
            + (coefficient[5] * x * y)
            + (coefficient[6] * x * x * x)
            + (coefficient[7] * y * y * y)
            + (coefficient[8] * x * x * y)
            + (coefficient[9] * x * y * y);
    }

    private static void Append(IdfDocument document, IEnumerable<IdfObject> objects)
    {
        foreach (IdfObject item in objects)
        {
            document.Append(item);
        }
    }
}
