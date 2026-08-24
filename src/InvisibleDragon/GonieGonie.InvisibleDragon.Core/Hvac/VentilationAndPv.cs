using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Internal;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Shape;

namespace GonieGonie.InvisibleDragon.Hvac;

public sealed record ZoneVentilationAssignment
{
    public ZoneVentilationAssignment(EntityId zoneId, EnergyRecoveryVentilator ventilator)
    {
        ZoneId = zoneId ?? throw new ArgumentNullException(nameof(zoneId));
        Ventilator = ventilator ?? throw new ArgumentNullException(nameof(ventilator));
    }

    public EntityId ZoneId { get; }

    public EnergyRecoveryVentilator Ventilator { get; }
}

/// <summary>
/// A stand-alone sensible/latent heat-recovery ventilator for one thermal zone.
/// </summary>
public sealed class EnergyRecoveryVentilator : HvacSystem
{
    public EnergyRecoveryVentilator(
        EntityId id,
        string name,
        double sensibleEffectiveness,
        double latentEffectiveness,
        double? supplyAirFlowCubicMetresPerSecond = null,
        double fanTotalEfficiency = 0.7,
        double fanPressureRisePascals = 100)
        : base(id, name)
    {
        SensibleEffectiveness = DomainGuard.InRange(sensibleEffectiveness, 0, 1, nameof(sensibleEffectiveness));
        LatentEffectiveness = DomainGuard.InRange(latentEffectiveness, 0, 1, nameof(latentEffectiveness));
        SupplyAirFlowCubicMetresPerSecond = supplyAirFlowCubicMetresPerSecond is null
            ? null
            : DomainGuard.Positive(supplyAirFlowCubicMetresPerSecond.Value, nameof(supplyAirFlowCubicMetresPerSecond));
        FanTotalEfficiency = DomainGuard.InRange(fanTotalEfficiency, 0.000001, 1, nameof(fanTotalEfficiency));
        FanPressureRisePascals = DomainGuard.Positive(fanPressureRisePascals, nameof(fanPressureRisePascals));
    }

    public double SensibleEffectiveness { get; }

    public double LatentEffectiveness { get; }

    public double? SupplyAirFlowCubicMetresPerSecond { get; }

    public double FanTotalEfficiency { get; }

    public double FanPressureRisePascals { get; }

    internal SupplyIdfFragment Generate(IdfGenerationContext context, Zone zone, string availabilityScheduleName)
    {
        string prefix = $"ERV_named_{Name}_for_{zone.Name}";
        object flow = SupplyAirFlowCubicMetresPerSecond ?? (object)"autosize";
        string supplyFan = $"{prefix} Supply Fan";
        string exhaustFan = $"{prefix} Exhaust Fan";
        string exchanger = $"{prefix} Heat Exchanger";
        string outdoor = $"{prefix} Outdoor Air Node";
        string supplyFanInlet = $"{prefix} Supply Fan Inlet Node";
        string supplyOutlet = $"{prefix} Supply Outlet Node";
        string exhaustInlet = $"{prefix} Exhaust Inlet Node";
        string exhaustFanInlet = $"{prefix} Exhaust Fan Inlet Node";
        string relief = $"{prefix} Relief Air Node";
        List<IdfObject> objects = new()
        {
            context.CreateRaw("OutdoorAir:Node", outdoor),
            context.CreateRaw("HeatExchanger:AirToAir:SensibleAndLatent", exchanger, availabilityScheduleName, flow, SensibleEffectiveness, LatentEffectiveness, SensibleEffectiveness, LatentEffectiveness, outdoor, supplyFanInlet, exhaustInlet, exhaustFanInlet),
            Fan(context, supplyFan, availabilityScheduleName, flow, supplyFanInlet, supplyOutlet),
            Fan(context, exhaustFan, availabilityScheduleName, flow, exhaustFanInlet, relief),
            context.CreateRaw("ZoneHVAC:EnergyRecoveryVentilator:Controller", $"{prefix} Controller"),
            context.Create(
                "ZoneHVAC:EnergyRecoveryVentilator",
                IdfGenerationContext.Field(0, "Name", prefix),
                IdfGenerationContext.Field(1, "Availability Schedule Name", availabilityScheduleName),
                IdfGenerationContext.Field(2, "Heat Exchanger Name", exchanger),
                IdfGenerationContext.Field(3, "Supply Air Flow Rate", flow),
                IdfGenerationContext.Field(4, "Exhaust Air Flow Rate", flow),
                IdfGenerationContext.Field(5, "Supply Air Fan Name", supplyFan),
                IdfGenerationContext.Field(6, "Exhaust Air Fan Name", exhaustFan),
                IdfGenerationContext.Field(7, "Controller Name", $"{prefix} Controller")),
        };
        return new SupplyIdfFragment(
            objects,
            new ZoneEquipmentDescriptor("ZoneHVAC:EnergyRecoveryVentilator", prefix, 1, 1, supplyOutlet, exhaustInlet));
    }

    private IdfObject Fan(IdfGenerationContext context, string name, string schedule, object flow, string inlet, string outlet) =>
        context.CreateRaw("Fan:OnOff", name, schedule, FanTotalEfficiency, FanPressureRisePascals, flow, 0.9, 1, inlet, outlet);
}

/// <summary>
/// A fixed-geometry, simple-performance photovoltaic panel and load center.
/// </summary>
public sealed class PhotovoltaicPanel : HvacSystem
{
    public PhotovoltaicPanel(
        EntityId id,
        string name,
        double areaSquareMetres,
        double tiltDegrees,
        double azimuthDegrees,
        double efficiency,
        double activeCellAreaFraction = 0.7)
        : base(id, name)
    {
        AreaSquareMetres = DomainGuard.Positive(areaSquareMetres, nameof(areaSquareMetres));
        TiltDegrees = DomainGuard.InRange(tiltDegrees, 0, 90, nameof(tiltDegrees));
        AzimuthDegrees = DomainGuard.InRange(azimuthDegrees, 0, 359.999999, nameof(azimuthDegrees));
        Efficiency = DomainGuard.InRange(efficiency, 0.000001, 1, nameof(efficiency));
        ActiveCellAreaFraction = DomainGuard.InRange(activeCellAreaFraction, 0.000001, 1, nameof(activeCellAreaFraction));
    }

    public double AreaSquareMetres { get; }

    public double TiltDegrees { get; }

    public double AzimuthDegrees { get; }

    public double Efficiency { get; }

    public double ActiveCellAreaFraction { get; }

    public IReadOnlyList<IdfObject> ToIdfObjects(IdfGenerationContext context)
    {
        double side = Math.Sqrt(AreaSquareMetres);
        string shade = $"Shading4PVpanel:{Name}";
        string performance = $"Spec4PVpanel:{Name}";
        string panel = $"PVpanel:{Name}";
        string generators = $"Generator4PVpanel:{Name}";
        string inverter = $"Inverter4PVpanel:{Name}";
        return new[]
        {
            context.CreateRaw("Shading:Site", shade, AzimuthDegrees, TiltDegrees, 0, 0, 10, side, side),
            context.CreateRaw("PhotovoltaicPerformance:Simple", performance, ActiveCellAreaFraction, "Fixed", Efficiency),
            context.CreateRaw("Generator:Photovoltaic", panel, shade, "PhotovoltaicPerformance:Simple", performance),
            context.CreateRaw("ElectricLoadCenter:Generators", generators, panel, "Generator:Photovoltaic", 1000000),
            context.CreateRaw("ElectricLoadCenter:Inverter:Simple", inverter, "ALLON", null, 0, 1),
            context.CreateRaw("ElectricLoadCenter:Distribution", $"Distribution4PVpanel:{Name}", generators, "Baseload", 1000000, null, null, "DirectCurrentWithInverter", inverter),
        };
    }
}
