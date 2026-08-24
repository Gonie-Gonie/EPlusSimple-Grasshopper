using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Internal;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Shape;

namespace GonieGonie.InvisibleDragon.Hvac;

/// <summary>
/// A four-pipe fan-coil terminal connected to one upstream-compatible heating or cooling source.
/// </summary>
public sealed class FanCoilUnit : SupplySystem
{
    private const string ObjectType = "ZoneHVAC:FourPipeFanCoil";
    private const string HeatingCoilObjectType = "Coil:Heating:Water";

    public FanCoilUnit(
        EntityId id,
        string name,
        SourceSystem source,
        double fanTotalEfficiency = 0.7,
        double fanPressureRisePascals = 100,
        double motorEfficiency = 0.9)
        : base(id, name, RequireHydronicSource(source))
    {
        FanTotalEfficiency = DomainGuard.InRange(
            fanTotalEfficiency,
            0.000001,
            1,
            nameof(fanTotalEfficiency));
        FanPressureRisePascals = DomainGuard.Positive(
            fanPressureRisePascals,
            nameof(fanPressureRisePascals));
        MotorEfficiency = DomainGuard.InRange(motorEfficiency, 0.000001, 1, nameof(motorEfficiency));
    }

    public double FanTotalEfficiency { get; }

    public double FanPressureRisePascals { get; }

    public double MotorEfficiency { get; }

    public override bool CanHeat => Source is Boiler or DistrictHeating;

    public override bool CanCool => Source is Chiller or AbsorptionChiller;

    internal override SupplyIdfFragment Generate(
        IdfGenerationContext context,
        Zone zone,
        string availabilityScheduleName)
    {
        DomainGuard.NotNull(context, nameof(context));
        DomainGuard.NotNull(zone, nameof(zone));
        availabilityScheduleName = DomainGuard.RequiredText(
            availabilityScheduleName,
            nameof(availabilityScheduleName));

        string name = ObjectNameFor(zone);
        string airInlet = $"{name} Air InletNode";
        string mixedAir = $"{name} OAmixer2Fan Air MiddleNode";
        string fanOutlet = $"{name} Fan2CoolingCoil Air MiddleNode";
        string coolingOutlet = $"{name} CoolingCoil2HeatingCoil Air MiddleNode";
        string airOutlet = $"{name} Air OutletNode";
        string mixerName = $"OAmixer_for_{name}";
        string outdoorAirNode = $"{mixerName} Air OutdoorNode";
        string reliefAirNode = $"{mixerName} Air ReliefNode";
        string fanName = $"Fan_for_{name}";
        string coolingCoilName = $"CoolingCoil_for_{name}";
        string coolingWaterInlet = $"{coolingCoilName} Water InletNode";
        string coolingWaterOutlet = $"{coolingCoilName} Water OutletNode";
        string heatingCoilName = $"HeatingCoil_for_{name}";
        string heatingWaterInlet = $"{heatingCoilName} Water InletNode";
        string heatingWaterOutlet = $"{heatingCoilName} Water OutletNode";
        string powerCurve = $"Curve_for_{name}:PowerSpeedRatio";
        string efficiencyCurve = $"Curve_for_{name}:EffSpeedRatio";

        List<IdfObject> objects = new()
        {
            context.CreateRaw("Curve:Exponent", powerCurve, 0, 1, 3, 0, 1.5, 0.01, 1.5),
            context.CreateRaw(
                "Curve:Cubic",
                efficiencyCurve,
                0.33856828,
                1.72644131,
                -1.49280132,
                0.42776208,
                0,
                1.5,
                0.3,
                1),
            context.Create(
                "OutdoorAir:Mixer",
                IdfGenerationContext.Field(0, "Name", mixerName),
                IdfGenerationContext.Field(1, "Mixed Air Node Name", mixedAir),
                IdfGenerationContext.Field(2, "Outdoor Air Stream Node Name", outdoorAirNode),
                IdfGenerationContext.Field(3, "Relief Air Stream Node Name", reliefAirNode),
                IdfGenerationContext.Field(4, "Return Air Stream Node Name", airInlet)),
            context.CreateRaw("OutdoorAir:NodeList", outdoorAirNode),
            context.Create(
                "Fan:OnOff",
                IdfGenerationContext.Field(0, "Name", fanName),
                IdfGenerationContext.Field(1, "Availability Schedule Name", availabilityScheduleName),
                IdfGenerationContext.Field(2, "Fan Total Efficiency", FanTotalEfficiency),
                IdfGenerationContext.Field(3, "Pressure Rise", FanPressureRisePascals),
                IdfGenerationContext.Field(4, "Maximum Flow Rate", "autosize"),
                IdfGenerationContext.Field(5, "Motor Efficiency", MotorEfficiency),
                IdfGenerationContext.Field(6, "Motor In Airstream Fraction", 1),
                IdfGenerationContext.Field(7, "Air Inlet Node Name", mixedAir),
                IdfGenerationContext.Field(8, "Air Outlet Node Name", fanOutlet),
                IdfGenerationContext.Field(9, "Fan Power Ratio Function of Speed Ratio Curve Name", powerCurve),
                IdfGenerationContext.Field(10, "Fan Efficiency Ratio Function of Speed Ratio Curve Name", efficiencyCurve)),
            context.Create(
                "Coil:Cooling:Water",
                IdfGenerationContext.Field(0, "Name", coolingCoilName),
                IdfGenerationContext.Field(1, "Availability Schedule Name", CanCool ? availabilityScheduleName : "ALLOFF"),
                IdfGenerationContext.Field(2, "Design Water Flow Rate", CanCool ? "autosize" : (object)0),
                IdfGenerationContext.Field(3, "Design Air Flow Rate", "autosize"),
                IdfGenerationContext.Field(4, "Design Inlet Water Temperature", "autosize"),
                IdfGenerationContext.Field(5, "Design Inlet Air Temperature", "autosize"),
                IdfGenerationContext.Field(6, "Design Outlet Air Temperature", "autosize"),
                IdfGenerationContext.Field(7, "Design Inlet Air Humidity Ratio", "autosize"),
                IdfGenerationContext.Field(8, "Design Outlet Air Humidity Ratio", "autosize"),
                IdfGenerationContext.Field(9, "Water Inlet Node Name", coolingWaterInlet),
                IdfGenerationContext.Field(10, "Water Outlet Node Name", coolingWaterOutlet),
                IdfGenerationContext.Field(11, "Air Inlet Node Name", fanOutlet),
                IdfGenerationContext.Field(12, "Air Outlet Node Name", coolingOutlet),
                IdfGenerationContext.Field(13, "Type of Analysis", "SimpleAnalysis"),
                IdfGenerationContext.Field(14, "Heat Exchanger Configuration", "CounterFlow")),
            context.Create(
                HeatingCoilObjectType,
                IdfGenerationContext.Field(0, "Name", heatingCoilName),
                IdfGenerationContext.Field(1, "Availability Schedule Name", CanHeat ? availabilityScheduleName : "ALLOFF"),
                IdfGenerationContext.Field(2, "U-Factor Times Area Value", "autosize"),
                IdfGenerationContext.Field(3, "Maximum Water Flow Rate", CanHeat ? "autosize" : (object)0),
                IdfGenerationContext.Field(4, "Water Inlet Node Name", heatingWaterInlet),
                IdfGenerationContext.Field(5, "Water Outlet Node Name", heatingWaterOutlet),
                IdfGenerationContext.Field(6, "Air Inlet Node Name", coolingOutlet),
                IdfGenerationContext.Field(7, "Air Outlet Node Name", airOutlet),
                IdfGenerationContext.Field(8, "Performance Input Method", "UFactorTimesAreaAndDesignWaterFlowRate"),
                IdfGenerationContext.Field(9, "Rated Capacity", "autosize"),
                IdfGenerationContext.Field(10, "Rated Inlet Water Temperature", 82.2),
                IdfGenerationContext.Field(11, "Rated Inlet Air Temperature", 16.6),
                IdfGenerationContext.Field(12, "Rated Outlet Water Temperature", 71.1),
                IdfGenerationContext.Field(13, "Rated Outlet Air Temperature", 32.2),
                IdfGenerationContext.Field(14, "Rated Ratio for Air and Water Convection", 0.5)),
            context.Create(
                ObjectType,
                IdfGenerationContext.Field(0, "Name", name),
                IdfGenerationContext.Field(1, "Availability Schedule Name", availabilityScheduleName),
                IdfGenerationContext.Field(2, "Capacity Control Method", "ConstantFanVariableFlow"),
                IdfGenerationContext.Field(3, "Maximum Supply Air Flow Rate", "autosize"),
                IdfGenerationContext.Field(4, "Low Speed Supply Air Flow Ratio", 0.33),
                IdfGenerationContext.Field(5, "Medium Speed Supply Air Flow Ratio", 0.66),
                IdfGenerationContext.Field(6, "Maximum Outdoor Air Flow Rate", 0),
                IdfGenerationContext.Field(7, "Outdoor Air Schedule Name", availabilityScheduleName),
                IdfGenerationContext.Field(8, "Air Inlet Node Name", airInlet),
                IdfGenerationContext.Field(9, "Air Outlet Node Name", airOutlet),
                IdfGenerationContext.Field(10, "Outdoor Air Mixer Object Type", "OutdoorAir:Mixer"),
                IdfGenerationContext.Field(11, "Outdoor Air Mixer Name", mixerName),
                IdfGenerationContext.Field(12, "Supply Air Fan Object Type", "Fan:OnOff"),
                IdfGenerationContext.Field(13, "Supply Air Fan Name", fanName),
                IdfGenerationContext.Field(14, "Cooling Coil Object Type", "Coil:Cooling:Water"),
                IdfGenerationContext.Field(15, "Cooling Coil Name", coolingCoilName),
                IdfGenerationContext.Field(16, "Maximum Cold Water Flow Rate", CanCool ? "autosize" : (object)0),
                IdfGenerationContext.Field(17, "Minimum Cold Water Flow Rate", 0),
                IdfGenerationContext.Field(18, "Cooling Convergence Tolerance", 0.001),
                IdfGenerationContext.Field(19, "Heating Coil Object Type", HeatingCoilObjectType),
                IdfGenerationContext.Field(20, "Heating Coil Name", heatingCoilName),
                IdfGenerationContext.Field(21, "Maximum Hot Water Flow Rate", CanHeat ? "autosize" : (object)0),
                IdfGenerationContext.Field(22, "Minimum Hot Water Flow Rate", 0),
                IdfGenerationContext.Field(23, "Heating Convergence Tolerance", 0.001)),
        };

        var heatingConnection = new PlantDemandConnection(
            $"{Source!.LoopName} Demand {name}",
            HeatingCoilObjectType,
            heatingCoilName,
            heatingWaterInlet,
            heatingWaterOutlet);
        var coolingConnection = new PlantDemandConnection(
            $"{Source!.LoopName} Demand {name}",
            "Coil:Cooling:Water",
            coolingCoilName,
            coolingWaterInlet,
            coolingWaterOutlet);
        AppendOppositePlantLoop(
            objects,
            context,
            name,
            heatingConnection,
            coolingConnection);

        return new SupplyIdfFragment(
            objects,
            new ZoneEquipmentDescriptor(
                ObjectType,
                name,
                CanCool ? 1 : 0,
                CanHeat ? 1 : 0,
                airOutlet,
                airInlet),
            CanHeat ? heatingConnection : coolingConnection);
    }

    private void AppendOppositePlantLoop(
        List<IdfObject> objects,
        IdfGenerationContext context,
        string equipmentName,
        PlantDemandConnection heatingConnection,
        PlantDemandConnection coolingConnection)
    {
        if (CanHeat)
        {
            var auxiliary = new AuxiliaryDistrictCooling(
                new EntityId($"{Id.Value}-AUX-COOLING"),
                $"{equipmentName} Auxiliary Cooling");
            PlantDemandConnection branch = WithSourceLoop(auxiliary, equipmentName, coolingConnection);
            objects.AddRange(auxiliary.ToIdfObjects(context, new[] { branch }));
            return;
        }

        var auxiliaryHeating = new DistrictHeating(
            new EntityId($"{Id.Value}-AUX-HEATING"),
            $"{equipmentName} Auxiliary Heating",
            nominalCapacityWatts: 0.1);
        PlantDemandConnection heatingBranch = WithSourceLoop(
            auxiliaryHeating,
            equipmentName,
            heatingConnection);
        objects.AddRange(auxiliaryHeating.ToIdfObjects(context, new[] { heatingBranch }));
    }

    private static PlantDemandConnection WithSourceLoop(
        SourceSystem source,
        string equipmentName,
        PlantDemandConnection connection) => new(
            $"{source.LoopName} Demand {equipmentName}",
            connection.ComponentObjectType,
            connection.ComponentName,
            connection.InletNodeName,
            connection.OutletNodeName);

    private static SourceSystem RequireHydronicSource(SourceSystem? source)
    {
        source = DomainGuard.NotNull(source, nameof(source));

        if (source is not Boiler
            && source is not DistrictHeating
            && source is not Chiller
            && source is not AbsorptionChiller)
        {
            throw new ArgumentException(
                "A fan-coil unit requires a Boiler, DistrictHeating, Chiller, or AbsorptionChiller source.",
                nameof(source));
        }

        return source;
    }

    private sealed class AuxiliaryDistrictCooling : SourceSystem
    {
        internal AuxiliaryDistrictCooling(EntityId id, string name)
            : base(id, name)
        {
        }

        public override string IdfObjectType => "DistrictCooling";

        public override string IdfObjectName => $"DistrictCooling_named_{Name}";

        public override IReadOnlyList<IdfObject> ToIdfObjects(
            IdfGenerationContext context,
            IReadOnlyList<PlantDemandConnection>? demandConnections = null,
            IReadOnlyList<string>? terminalUnitNames = null)
        {
            DomainGuard.NotNull(context, nameof(context));
            string inlet = $"{IdfObjectName} Water InletNode";
            string outlet = $"{IdfObjectName} Water OutletNode";
            IdfObject component = context.Create(
                IdfObjectType,
                IdfGenerationContext.Field(0, "Name", IdfObjectName),
                IdfGenerationContext.Field(1, "Chilled Water Inlet Node Name", inlet),
                IdfGenerationContext.Field(2, "Chilled Water Outlet Node Name", outlet),
                IdfGenerationContext.Field(3, "Nominal Capacity", 0.1),
                IdfGenerationContext.Field(4, "Capacity Fraction Schedule Name", "ALLOFF"));
            return CoolingPlantLoopAssembler.CreateCoolingLoop(
                context,
                this,
                component,
                inlet,
                outlet,
                0.9,
                6,
                demandConnections ?? Array.Empty<PlantDemandConnection>());
        }
    }
}

/// <summary>
/// A radiant-convective hot-water baseboard connected to a heating plant.
/// </summary>
public sealed class Radiator : SupplySystem
{
    private const string ObjectType = "ZoneHVAC:Baseboard:RadiantConvective:Water";

    public Radiator(
        EntityId id,
        string name,
        SourceSystem source,
        double? heatingCapacityWatts = null,
        double radiantFraction = 0)
        : base(id, name, RequireHeatingSource(source))
    {
        HeatingCapacityWatts = heatingCapacityWatts is null
            ? null
            : DomainGuard.Positive(heatingCapacityWatts.Value, nameof(heatingCapacityWatts));
        RadiantFraction = DomainGuard.InRange(radiantFraction, 0, 1, nameof(radiantFraction));
    }

    public double? HeatingCapacityWatts { get; }

    public double RadiantFraction { get; }

    public override bool CanHeat => true;

    public override bool CanCool => false;

    internal override SupplyIdfFragment Generate(
        IdfGenerationContext context,
        Zone zone,
        string availabilityScheduleName)
    {
        DomainGuard.NotNull(context, nameof(context));
        DomainGuard.NotNull(zone, nameof(zone));
        availabilityScheduleName = DomainGuard.RequiredText(
            availabilityScheduleName,
            nameof(availabilityScheduleName));

        string name = ObjectNameFor(zone);
        string designName = $"DesignOf_{name}";
        string inlet = $"{name} Water InletNode";
        string outlet = $"{name} Water OutletNode";
        IdfObject design = context.Create(
            "ZoneHVAC:Baseboard:RadiantConvective:Water:Design",
            IdfGenerationContext.Field(0, "Name", designName),
            IdfGenerationContext.Field(1, "Heating Design Capacity Method", "HeatingDesignCapacity"),
            IdfGenerationContext.Field(2, "Heating Design Capacity Per Floor Area", null),
            IdfGenerationContext.Field(3, "Fraction of Autosized Heating Design Capacity", 1),
            IdfGenerationContext.Field(4, "Convergence Tolerance", 0.001),
            IdfGenerationContext.Field(5, "Fraction Radiant", RadiantFraction),
            IdfGenerationContext.Field(6, "Fraction of Radiant Energy Incident on People", 0));
        IdfObject radiator = context.Create(
            ObjectType,
            IdfGenerationContext.Field(0, "Name", name),
            IdfGenerationContext.Field(1, "Design Object", designName),
            IdfGenerationContext.Field(2, "Availability Schedule Name", availabilityScheduleName),
            IdfGenerationContext.Field(3, "Inlet Node Name", inlet),
            IdfGenerationContext.Field(4, "Outlet Node Name", outlet),
            IdfGenerationContext.Field(5, "Rated Average Water Temperature", 87.78),
            IdfGenerationContext.Field(6, "Rated Water Mass Flow Rate", 0.063),
            IdfGenerationContext.Field(7, "Heating Design Capacity", HeatingCapacityWatts ?? (object)"autosize"),
            IdfGenerationContext.Field(8, "Maximum Water Flow Rate", "autosize"));
        double totalSurfaceArea = zone.Surfaces.Sum(surface => surface.GrossArea);
        foreach (Surface surface in zone.Surfaces)
        {
            radiator.Add(surface.Name);
            radiator.Add(IdfGenerationContext.Format(surface.GrossArea / totalSurfaceArea));
        }

        return new SupplyIdfFragment(
            new[] { design, radiator },
            new ZoneEquipmentDescriptor(ObjectType, name, 0, 1),
            new PlantDemandConnection(
                $"{Source!.LoopName} Demand {name}",
                ObjectType,
                name,
                inlet,
                outlet));
    }

    private static SourceSystem RequireHeatingSource(SourceSystem? source)
    {
        source = DomainGuard.NotNull(source, nameof(source));

        if (source is not Boiler && source is not DistrictHeating)
        {
            throw new ArgumentException(
                "A hydronic radiator requires a Boiler or DistrictHeating source.",
                nameof(source));
        }

        return source;
    }
}
