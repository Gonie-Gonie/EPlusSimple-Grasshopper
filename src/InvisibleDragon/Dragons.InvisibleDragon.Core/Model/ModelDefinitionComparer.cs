using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Shape;
using OpaqueConstruction = Dragons.InvisibleDragon.Construction.Construction;

namespace Dragons.InvisibleDragon.Model;

internal static class ModelDefinitionComparer
{
    internal static bool LayerEquals(Layer first, Layer second)
    {
        return NameEquals(first.Name, second.Name)
            && first.ThicknessMetres.Equals(second.ThicknessMetres)
            && MaterialEquals(first.Material, second.Material);
    }

    internal static bool SurfaceConstructionEquals(
        ISurfaceConstruction first,
        ISurfaceConstruction second)
    {
        if (first.GetType() != second.GetType() || !NameEquals(first.Name, second.Name))
        {
            return false;
        }

        return (first, second) switch
        {
            (OpaqueConstruction left, OpaqueConstruction right) =>
                left.Layers.Count == right.Layers.Count
                && left.Layers.Zip(right.Layers, LayerEquals).All(equal => equal),
            (NoMassConstruction left, NoMassConstruction right) =>
                left.UValueWattsPerSquareMetreKelvin.Equals(right.UValueWattsPerSquareMetreKelvin),
            (AirBoundary left, AirBoundary right) =>
                left.AirChangesPerHour.Equals(right.AirChangesPerHour),
            _ => ReferenceEquals(first, second),
        };
    }

    internal static bool GlazingEquals(Glazing first, Glazing second)
    {
        return NameEquals(first.Name, second.Name)
            && first.UValueWattsPerSquareMetreKelvin.Equals(second.UValueWattsPerSquareMetreKelvin)
            && first.SolarHeatGainCoefficient.Equals(second.SolarHeatGainCoefficient);
    }

    internal static bool HvacSystemEquals(HvacSystem first, HvacSystem second)
    {
        if (first.GetType() != second.GetType() || !NameEquals(first.Name, second.Name))
        {
            return false;
        }

        return (first, second) switch
        {
            (HeatPump left, HeatPump right) =>
                left.Fuel == right.Fuel
                && left.HeatingCoefficientOfPerformance.Equals(right.HeatingCoefficientOfPerformance)
                && left.CoolingCoefficientOfPerformance.Equals(right.CoolingCoefficientOfPerformance)
                && Nullable.Equals(left.HeatingCapacityWatts, right.HeatingCapacityWatts)
                && Nullable.Equals(left.CoolingCapacityWatts, right.CoolingCapacityWatts),
            (Boiler left, Boiler right) =>
                left.Fuel == right.Fuel
                && left.NominalThermalEfficiency.Equals(right.NominalThermalEfficiency)
                && Nullable.Equals(left.NominalCapacityWatts, right.NominalCapacityWatts)
                && left.PumpMotorEfficiency.Equals(right.PumpMotorEfficiency)
                && left.SetpointTemperatureCelsius.Equals(right.SetpointTemperatureCelsius),
            (DistrictHeating left, DistrictHeating right) =>
                Nullable.Equals(left.NominalCapacityWatts, right.NominalCapacityWatts)
                && left.PumpMotorEfficiency.Equals(right.PumpMotorEfficiency)
                && left.SetpointTemperatureCelsius.Equals(right.SetpointTemperatureCelsius),
            (Chiller left, Chiller right) =>
                left.ReferenceCoefficientOfPerformance.Equals(right.ReferenceCoefficientOfPerformance)
                && left.Compressor == right.Compressor
                && Nullable.Equals(left.NominalCapacityWatts, right.NominalCapacityWatts)
                && left.PumpMotorEfficiency.Equals(right.PumpMotorEfficiency)
                && left.SetpointTemperatureCelsius.Equals(right.SetpointTemperatureCelsius)
                && CoolingTowerEquals(left.CoolingTower, right.CoolingTower),
            (AbsorptionChiller left, AbsorptionChiller right) =>
                left.ThermalCoefficientOfPerformance.Equals(right.ThermalCoefficientOfPerformance)
                && Nullable.Equals(left.NominalCapacityWatts, right.NominalCapacityWatts)
                && left.PumpMotorEfficiency.Equals(right.PumpMotorEfficiency)
                && left.SetpointTemperatureCelsius.Equals(right.SetpointTemperatureCelsius)
                && left.HeatSource.Id.Equals(right.HeatSource.Id)
                && HvacSystemEquals(left.HeatSource, right.HeatSource)
                && CoolingTowerEquals(left.CoolingTower, right.CoolingTower),
            (FanCoilUnit left, FanCoilUnit right) =>
                SourceEquals(left.Source, right.Source)
                && left.FanTotalEfficiency.Equals(right.FanTotalEfficiency)
                && left.FanPressureRisePascals.Equals(right.FanPressureRisePascals)
                && left.MotorEfficiency.Equals(right.MotorEfficiency),
            (Radiator left, Radiator right) =>
                SourceEquals(left.Source, right.Source)
                && Nullable.Equals(left.HeatingCapacityWatts, right.HeatingCapacityWatts)
                && left.RadiantFraction.Equals(right.RadiantFraction),
            (AirHandlingUnit left, AirHandlingUnit right) =>
                SourceEquals(left.Source, right.Source)
                && left.FanTotalEfficiency.Equals(right.FanTotalEfficiency)
                && left.FanPressureRisePascals.Equals(right.FanPressureRisePascals)
                && left.MotorEfficiency.Equals(right.MotorEfficiency),
            (RadiantFloor left, RadiantFloor right) =>
                SourceEquals(left.Source, right.Source)
                && left.ThrottlingRangeCelsius.Equals(right.ThrottlingRangeCelsius),
            (ElectricRadiantFloor left, ElectricRadiantFloor right) =>
                left.ThrottlingRangeCelsius.Equals(right.ThrottlingRangeCelsius),
            (ElectricRadiator left, ElectricRadiator right) =>
                Nullable.Equals(left.HeatingCapacityWatts, right.HeatingCapacityWatts)
                && left.Efficiency.Equals(right.Efficiency)
                && left.RadiantFraction.Equals(right.RadiantFraction),
            (EnergyRecoveryVentilator left, EnergyRecoveryVentilator right) =>
                left.SensibleEffectiveness.Equals(right.SensibleEffectiveness)
                && left.LatentEffectiveness.Equals(right.LatentEffectiveness)
                && Nullable.Equals(
                    left.SupplyAirFlowCubicMetresPerSecond,
                    right.SupplyAirFlowCubicMetresPerSecond)
                && left.FanTotalEfficiency.Equals(right.FanTotalEfficiency)
                && left.FanPressureRisePascals.Equals(right.FanPressureRisePascals),
            (PhotovoltaicPanel left, PhotovoltaicPanel right) =>
                left.AreaSquareMetres.Equals(right.AreaSquareMetres)
                && left.TiltDegrees.Equals(right.TiltDegrees)
                && left.AzimuthDegrees.Equals(right.AzimuthDegrees)
                && left.Efficiency.Equals(right.Efficiency)
                && left.ActiveCellAreaFraction.Equals(right.ActiveCellAreaFraction),
            _ => ReferenceEquals(first, second),
        };
    }

    internal static bool EmittedMaterialEquals(object first, object second)
    {
        return (first, second) switch
        {
            (Layer left, Layer right) => LayerEquals(left, right),
            (NoMassConstruction left, NoMassConstruction right) =>
                SurfaceConstructionEquals(left, right),
            (Glazing left, Glazing right) => GlazingEquals(left, right),
            (Blind left, Blind right) =>
                NameEquals(left.Name, right.Name)
                && left.SlatWidthMetres.Equals(right.SlatWidthMetres)
                && left.SlatSeparationMetres.Equals(right.SlatSeparationMetres)
                && left.SlatAngleDegrees.Equals(right.SlatAngleDegrees)
                && left.FrontReflectance.Equals(right.FrontReflectance)
                && left.BackReflectance.Equals(right.BackReflectance),
            (Shade left, Shade right) =>
                NameEquals(left.Name, right.Name)
                && left.Transmittance.Equals(right.Transmittance)
                && left.Reflectance.Equals(right.Reflectance),
            _ => false,
        };
    }

    internal static bool EmittedConstructionEquals(object first, object second)
    {
        return (first, second) switch
        {
            (ISurfaceConstruction left, ISurfaceConstruction right) =>
                SurfaceConstructionEquals(left, right),
            (Glazing left, Glazing right) => GlazingEquals(left, right),
            _ => false,
        };
    }

    private static bool MaterialEquals(Material first, Material second)
    {
        return first.ConductivityWattsPerMetreKelvin.Equals(second.ConductivityWattsPerMetreKelvin)
            && first.DensityKilogramsPerCubicMetre.Equals(second.DensityKilogramsPerCubicMetre)
            && first.SpecificHeatJoulesPerKilogramKelvin.Equals(second.SpecificHeatJoulesPerKilogramKelvin)
            && first.ThermalAbsorptance.Equals(second.ThermalAbsorptance)
            && first.SolarAbsorptance.Equals(second.SolarAbsorptance)
            && first.VisibleAbsorptance.Equals(second.VisibleAbsorptance)
            && first.Roughness == second.Roughness;
    }

    private static bool SourceEquals(SourceSystem? first, SourceSystem? second)
    {
        return first is null ? second is null : second is not null && first.Id.Equals(second.Id);
    }

    private static bool CoolingTowerEquals(CoolingTower first, CoolingTower second)
    {
        return first.GetType() == second.GetType()
            && first.Id.Equals(second.Id)
            && NameEquals(first.Name, second.Name)
            && Nullable.Equals(first.NominalCapacityWatts, second.NominalCapacityWatts)
            && first.PumpMotorEfficiency.Equals(second.PumpMotorEfficiency);
    }

    private static bool NameEquals(string first, string second)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(first, second);
    }
}
