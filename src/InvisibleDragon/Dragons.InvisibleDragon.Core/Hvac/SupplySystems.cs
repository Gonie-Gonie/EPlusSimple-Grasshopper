using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Internal;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Shape;

namespace Dragons.InvisibleDragon.Hvac;

/// <summary>
/// A draw-through VRF indoor terminal with cooling and heating coils.
/// </summary>
public class AirHandlingUnit : SupplySystem
{
    public AirHandlingUnit(
        EntityId id,
        string name,
        HeatPump source,
        double fanTotalEfficiency = 0.7,
        double fanPressureRisePascals = 100,
        double motorEfficiency = 0.9)
        : base(id, name, source ?? throw new ArgumentNullException(nameof(source)))
    {
        FanTotalEfficiency = DomainGuard.InRange(fanTotalEfficiency, 0.000001, 1, nameof(fanTotalEfficiency));
        FanPressureRisePascals = DomainGuard.Positive(fanPressureRisePascals, nameof(fanPressureRisePascals));
        MotorEfficiency = DomainGuard.InRange(motorEfficiency, 0.000001, 1, nameof(motorEfficiency));
    }

    public double FanTotalEfficiency { get; }

    public double FanPressureRisePascals { get; }

    public double MotorEfficiency { get; }

    public override bool CanHeat => true;

    public override bool CanCool => true;

    internal override SupplyIdfFragment Generate(
        IdfGenerationContext context,
        Zone zone,
        string availabilityScheduleName)
    {
        DomainGuard.NotNull(context, nameof(context));
        DomainGuard.NotNull(zone, nameof(zone));
        string name = ObjectNameFor(zone);
        string inlet = $"{name} Air InletNode";
        string coilMiddle = $"{name} CoolingCoil2HeatingCoil Air MiddleNode";
        string fanMiddle = $"{name} HeatingCoil2Fan Air MiddleNode";
        string outlet = $"{name} Air OutletNode";
        string coolingCoil = $"CoolingCoil_for_{name}";
        string heatingCoil = $"HeatingCoil_for_{name}";
        string fan = $"Fan_for_{name}";
        double flow = Math.Max(zone.FloorArea * 0.01, 0.001);
        List<IdfObject> objects = new()
        {
            context.CreateRaw("Curve:Cubic", Curve(name, "HeatingCapaTemp"), -0.390708928, 0.261815024, -0.0130431603, 0.000178131746, 0, 50, 0.5, 1.5, "Temperature", "Dimensionless"),
            context.CreateRaw("Curve:Linear", Curve(name, "HeatingCapaFlow"), 0.8, 0.2, 0, 1.5),
            context.CreateRaw("Curve:Cubic", Curve(name, "CoolingCapaTemp"), 0.504547274, 0.0288891279, -0.0000108194187, 0.0000101359395, 0, 50, 0.5, 1.5, "Temperature", "Dimensionless"),
            context.CreateRaw("Curve:Linear", Curve(name, "CoolingCapaFlow"), 0.8, 0.2, 0, 1.5),
            context.Create(
                "Coil:Cooling:DX:VariableRefrigerantFlow",
                IdfGenerationContext.Field(0, "Name", coolingCoil),
                IdfGenerationContext.Field(1, "Availability Schedule Name", CanCool ? availabilityScheduleName : "ALLOFF"),
                IdfGenerationContext.Field(2, "Gross Rated Total Cooling Capacity", CanCool ? "autosize" : (object)0.1),
                IdfGenerationContext.Field(3, "Gross Rated Sensible Heat Ratio", 0.7),
                IdfGenerationContext.Field(4, "Rated Air Flow Rate", flow),
                IdfGenerationContext.Field(5, "Coil Air Inlet Node", inlet),
                IdfGenerationContext.Field(6, "Coil Air Outlet Node", coilMiddle),
                IdfGenerationContext.Field(7, "Cooling Capacity Ratio Modifier Function of Temperature Curve Name", Curve(name, "CoolingCapaTemp")),
                IdfGenerationContext.Field(8, "Cooling Capacity Modifier Curve Function of Flow Fraction Name", Curve(name, "CoolingCapaFlow"))),
            context.Create(
                "Coil:Heating:DX:VariableRefrigerantFlow",
                IdfGenerationContext.Field(0, "Name", heatingCoil),
                IdfGenerationContext.Field(1, "Availability Schedule", CanHeat ? availabilityScheduleName : "ALLOFF"),
                IdfGenerationContext.Field(2, "Gross Rated Heating Capacity", CanHeat ? "autosize" : (object)0.1),
                IdfGenerationContext.Field(3, "Rated Air Flow Rate", flow),
                IdfGenerationContext.Field(4, "Coil Air Inlet Node", coilMiddle),
                IdfGenerationContext.Field(5, "Coil Air Outlet Node", fanMiddle),
                IdfGenerationContext.Field(6, "Heating Capacity Ratio Modifier Function of Temperature Curve Name", Curve(name, "HeatingCapaTemp")),
                IdfGenerationContext.Field(7, "Heating Capacity Modifier Function of Flow Fraction Curve Name", Curve(name, "HeatingCapaFlow"))),
            context.Create(
                "Fan:ConstantVolume",
                IdfGenerationContext.Field(0, "Name", fan),
                IdfGenerationContext.Field(1, "Availability Schedule Name", availabilityScheduleName),
                IdfGenerationContext.Field(2, "Fan Total Efficiency", FanTotalEfficiency),
                IdfGenerationContext.Field(3, "Pressure Rise", FanPressureRisePascals),
                IdfGenerationContext.Field(4, "Maximum Flow Rate", flow),
                IdfGenerationContext.Field(6, "Motor Efficiency", MotorEfficiency),
                IdfGenerationContext.Field(8, "Air Inlet Node Name", fanMiddle),
                IdfGenerationContext.Field(9, "Air Outlet Node Name", outlet)),
            context.Create(
                "ZoneHVAC:TerminalUnit:VariableRefrigerantFlow",
                IdfGenerationContext.Field(0, "Zone Terminal Unit Name", name),
                IdfGenerationContext.Field(1, "Terminal Unit Availability Schedule", availabilityScheduleName),
                IdfGenerationContext.Field(2, "Terminal Unit Air Inlet Node Name", inlet),
                IdfGenerationContext.Field(3, "Terminal Unit Air Outlet Node Name", outlet),
                IdfGenerationContext.Field(4, "Cooling Supply Air Flow Rate", "autosize"),
                IdfGenerationContext.Field(5, "No Cooling Supply Air Flow Rate", "autosize"),
                IdfGenerationContext.Field(6, "Heating Supply Air Flow Rate", "autosize"),
                IdfGenerationContext.Field(7, "No Heating Supply Air Flow Rate", "autosize"),
                IdfGenerationContext.Field(8, "Cooling Outdoor Air Flow Rate", 0),
                IdfGenerationContext.Field(9, "Heating Outdoor Air Flow Rate", 0),
                IdfGenerationContext.Field(10, "No Load Outdoor Air Flow Rate", 0),
                IdfGenerationContext.Field(14, "Supply Air Fan Operating Mode Schedule Name", "ALLON"),
                IdfGenerationContext.Field(15, "Supply Air Fan Placement", "DrawThrough"),
                IdfGenerationContext.Field(17, "Supply Air Fan Object Name", fan),
                IdfGenerationContext.Field(18, "Cooling Coil Object Type", "Coil:Cooling:DX:VariableRefrigerantFlow"),
                IdfGenerationContext.Field(19, "Cooling Coil Object Name", coolingCoil),
                IdfGenerationContext.Field(20, "Heating Coil Object Type", "Coil:Heating:DX:VariableRefrigerantFlow"),
                IdfGenerationContext.Field(21, "Heating Coil Object Name", heatingCoil),
                IdfGenerationContext.Field(24, "Zone Terminal Unit On Parasitic Electric Energy Use", 30),
                IdfGenerationContext.Field(25, "Zone Terminal Unit Off Parasitic Electric Energy Use", 20)),
        };

        return new SupplyIdfFragment(
            objects,
            new ZoneEquipmentDescriptor("ZoneHVAC:TerminalUnit:VariableRefrigerantFlow", name, 1, 1, outlet, inlet),
            terminalUnitName: name);
    }

    private static string Curve(string name, string suffix) => $"Curve_for_{name}:{suffix}";
}

/// <summary>
/// Explicit VRF terminology for an <see cref="AirHandlingUnit"/> indoor terminal.
/// </summary>
public sealed class VariableRefrigerantFlowTerminal : AirHandlingUnit
{
    public VariableRefrigerantFlowTerminal(EntityId id, string name, HeatPump source)
        : base(id, name, source)
    {
    }
}

/// <summary>
/// Cooling-only packaged terminal on a VRF heat-pump source.
/// </summary>
public sealed class PackagedAirConditioner : AirHandlingUnit
{
    public PackagedAirConditioner(EntityId id, string name, HeatPump source)
        : base(id, name, source)
    {
    }

    public override bool CanHeat => false;
}

/// <summary>
/// Hydronic low-temperature radiant floor connected to a heating plant.
/// </summary>
public sealed class RadiantFloor : SupplySystem
{
    public RadiantFloor(EntityId id, string name, SourceSystem source, double throttlingRangeCelsius = 2)
        : base(id, name, source ?? throw new ArgumentNullException(nameof(source)))
    {
        if (source is HeatPump)
        {
            throw new ArgumentException("A radiant floor requires a hydronic source.", nameof(source));
        }

        ThrottlingRangeCelsius = DomainGuard.Positive(throttlingRangeCelsius, nameof(throttlingRangeCelsius));
    }

    public double ThrottlingRangeCelsius { get; }

    public override bool CanHeat => true;

    public override bool CanCool => false;

    internal override SupplyIdfFragment Generate(IdfGenerationContext context, Zone zone, string availabilityScheduleName)
    {
        DomainGuard.NotNull(context, nameof(context));
        RequireRadiantZone(zone);
        string name = ObjectNameFor(zone);
        string group = $"RadiantFloorSurfaceGroup_for_{zone.Name}";
        List<IdfObject> objects = InternalHeatSourceObjects(context, zone).ToList();
        objects.Add(SurfaceGroup(context, zone, group));
        objects.Add(context.Create(
            "ZoneHVAC:LowTemperatureRadiant:VariableFlow:Design",
            IdfGenerationContext.Field(0, "Name", $"DesignOf_{name}"),
            IdfGenerationContext.Field(1, "Heating Design Capacity Method", "HeatingDesignCapacity"),
            IdfGenerationContext.Field(11, "Heating Control Temperature Schedule Name", zone.Profile.HeatingSetpoint!.Name),
            IdfGenerationContext.Field(12, "Heating Control Throttling Range", ThrottlingRangeCelsius),
            IdfGenerationContext.Field(17, "Setpoint Control Type", "ZeroFlowPower")));
        string inlet = $"{name} Water InletNode";
        string outlet = $"{name} Water OutletNode";
        objects.Add(context.Create(
            "ZoneHVAC:LowTemperatureRadiant:VariableFlow",
            IdfGenerationContext.Field(0, "Name", name),
            IdfGenerationContext.Field(1, "Design Object", $"DesignOf_{name}"),
            IdfGenerationContext.Field(2, "Availability Schedule Name", availabilityScheduleName),
            IdfGenerationContext.Field(3, "Zone Name", zone.Name),
            IdfGenerationContext.Field(4, "Surface Name or Radiant Surface Group Name", group),
            IdfGenerationContext.Field(5, "Maximum Hot Water Flow", "autosize"),
            IdfGenerationContext.Field(6, "Heating Water Inlet Node Name", inlet),
            IdfGenerationContext.Field(7, "Heating Water Outlet Node Name", outlet)));
        return new SupplyIdfFragment(
            objects,
            new ZoneEquipmentDescriptor("ZoneHVAC:LowTemperatureRadiant:VariableFlow", name, 0, 1),
            new PlantDemandConnection(
                $"{Source!.LoopName} Demand Main_{nameof(RadiantFloor)}_for_{zone.Name}",
                "ZoneHVAC:LowTemperatureRadiant:VariableFlow",
                name,
                inlet,
                outlet));
    }

    internal static IEnumerable<IdfObject> InternalHeatSourceObjects(IdfGenerationContext context, Zone zone)
    {
        foreach (Surface surface in zone.FloorSurfaces)
        {
            if (surface.Construction is not Construction.Construction construction)
            {
                continue;
            }

            int sourceLayer = Math.Max(construction.Layers.Count - 1, 1);
            yield return context.CreateRaw(
                "ConstructionProperty:InternalHeatSource",
                $"{surface.Name} Internal Heat Source",
                $"{surface.Construction.Name}:for:{surface.Name}",
                sourceLayer,
                sourceLayer,
                1,
                0.3);
        }
    }

    internal static IdfObject SurfaceGroup(IdfGenerationContext context, Zone zone, string groupName)
    {
        List<object?> values = new() { groupName };
        double area = zone.FloorSurfaces.Sum(surface => surface.GrossArea);
        foreach (Surface floor in zone.FloorSurfaces)
        {
            values.Add(floor.Name);
            values.Add(floor.GrossArea / area);
        }

        return context.CreateRaw("ZoneHVAC:LowTemperatureRadiant:SurfaceGroup", values.ToArray());
    }

    internal static void RequireRadiantZone(Zone zone)
    {
        DomainGuard.NotNull(zone, nameof(zone));
        if (zone.FloorSurfaces.Count == 0)
        {
            throw new InvalidOperationException($"Zone '{zone.Name}' has no floor for radiant equipment.");
        }

        if (zone.Profile.HeatingSetpoint is null)
        {
            throw new InvalidOperationException($"Zone '{zone.Name}' has no heating setpoint schedule.");
        }
    }
}

/// <summary>
/// Electric low-temperature radiant floor without a plant source.
/// </summary>
public sealed class ElectricRadiantFloor : SupplySystem
{
    public ElectricRadiantFloor(EntityId id, string name, double throttlingRangeCelsius = 2)
        : base(id, name, null)
    {
        ThrottlingRangeCelsius = DomainGuard.Positive(throttlingRangeCelsius, nameof(throttlingRangeCelsius));
    }

    public double ThrottlingRangeCelsius { get; }

    public override bool CanHeat => true;

    public override bool CanCool => false;

    internal override SupplyIdfFragment Generate(IdfGenerationContext context, Zone zone, string availabilityScheduleName)
    {
        DomainGuard.NotNull(context, nameof(context));
        RadiantFloor.RequireRadiantZone(zone);
        string name = ObjectNameFor(zone);
        string group = context.Options.UseLegacySimpleDragonHvacTopology
            ? $"RadiantFloorSurfaceGroup_for_{zone.Name}"
            : $"ElectricRadiantFloorSurfaceGroup_for_{zone.Name}";
        List<IdfObject> objects = RadiantFloor.InternalHeatSourceObjects(context, zone).ToList();
        objects.Add(RadiantFloor.SurfaceGroup(context, zone, group));
        objects.Add(context.Create(
            "ZoneHVAC:LowTemperatureRadiant:Electric",
            IdfGenerationContext.Field(0, "Name", name),
            IdfGenerationContext.Field(1, "Availability Schedule Name", availabilityScheduleName),
            IdfGenerationContext.Field(2, "Zone Name", zone.Name),
            IdfGenerationContext.Field(3, "Surface Name or Radiant Surface Group Name", group),
            IdfGenerationContext.Field(4, "Heating Design Capacity Method", "HeatingDesignCapacity"),
            IdfGenerationContext.Field(5, "Heating Design Capacity", "autosize"),
            IdfGenerationContext.Field(8, "Temperature Control Type", "MeanAirTemperature"),
            IdfGenerationContext.Field(
                9,
                "Setpoint Control Type",
                context.Options.UseLegacySimpleDragonHvacTopology
                    ? "ZeroFlowPower"
                    : "HalfFlowPower"),
            IdfGenerationContext.Field(10, "Heating Throttling Range", ThrottlingRangeCelsius),
            IdfGenerationContext.Field(11, "Heating Setpoint Temperature Schedule Name", zone.Profile.HeatingSetpoint!.Name)));
        return new SupplyIdfFragment(
            objects,
            new ZoneEquipmentDescriptor("ZoneHVAC:LowTemperatureRadiant:Electric", name, 0, 1));
    }
}

/// <summary>
/// Electric radiant-convective baseboard for simple heating-only cases.
/// </summary>
public sealed class ElectricRadiator : SupplySystem
{
    public ElectricRadiator(
        EntityId id,
        string name,
        double? heatingCapacityWatts = null,
        double efficiency = 1,
        double radiantFraction = 0)
        : base(id, name, null)
    {
        HeatingCapacityWatts = heatingCapacityWatts is null
            ? null
            : DomainGuard.Positive(heatingCapacityWatts.Value, nameof(heatingCapacityWatts));
        Efficiency = DomainGuard.InRange(efficiency, 0.000001, 1, nameof(efficiency));
        RadiantFraction = DomainGuard.InRange(radiantFraction, 0, 1, nameof(radiantFraction));
    }

    public double? HeatingCapacityWatts { get; }

    public double Efficiency { get; }

    public double RadiantFraction { get; }

    public override bool CanHeat => true;

    public override bool CanCool => false;

    internal override SupplyIdfFragment Generate(IdfGenerationContext context, Zone zone, string availabilityScheduleName)
    {
        DomainGuard.NotNull(context, nameof(context));
        DomainGuard.NotNull(zone, nameof(zone));
        availabilityScheduleName = DomainGuard.RequiredText(
            availabilityScheduleName,
            nameof(availabilityScheduleName));
        string name = ObjectNameFor(zone);
        var fields = new List<IdfFieldValue>
        {
            IdfGenerationContext.Field(0, "Name", name),
            IdfGenerationContext.Field(1, "Availability Schedule Name", availabilityScheduleName),
            IdfGenerationContext.Field(2, "Heating Design Capacity Method", "HeatingDesignCapacity"),
            IdfGenerationContext.Field(3, "Heating Design Capacity", HeatingCapacityWatts ?? (object)"autosize"),
            IdfGenerationContext.Field(4, "Heating Design Capacity Per Floor Area", null),
            IdfGenerationContext.Field(5, "Fraction of Autosized Heating Design Capacity", 1),
            IdfGenerationContext.Field(6, "Efficiency", Efficiency),
            IdfGenerationContext.Field(7, "Fraction Radiant", RadiantFraction),
        };
        if (!context.Options.UseLegacySimpleDragonHvacTopology)
        {
            fields.Add(IdfGenerationContext.Field(
                8,
                "Fraction of Radiant Energy Incident on People",
                0));
        }

        IdfObject radiator = context.Create(
            "ZoneHVAC:Baseboard:RadiantConvective:Electric",
            fields.ToArray());
        return new SupplyIdfFragment(
            new[] { radiator },
            new ZoneEquipmentDescriptor(radiator.ObjectType, name, 0, 1));
    }
}
