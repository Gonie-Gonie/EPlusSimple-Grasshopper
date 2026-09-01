namespace Dragons.SimpleDragon;

/// <summary>
/// Culture-independent unit conversion factors used by SimpleDragon.
/// </summary>
public static class UnitConversions
{
    public const double MillimetresToMetres = 1d / 1000d;
    public const double MetresToMillimetres = 1000d;
    public const double FractionToPercent = 100d;
    public const double PercentToFraction = 1d / 100d;
    public const double WattsToKilowatts = 1d / 1000d;
    public const double AirChangesAt50PaToNaturalAirChanges = 0.07d;
    public const double CubicMetresPerSecondToPerHour = 3600d;
}

/// <summary>
/// Surface convection coefficients used by the Korean regulation database.
/// </summary>
public static class ConvectionHeatTransfer
{
    public const double Interior = 1d / 0.110d;
    public const double Exterior = 1d / 0.043d;
}

/// <summary>
/// Energy carriers represented by the upstream SimpleDragon factors.
/// </summary>
public enum EnergyCarrier
{
    Electricity,
    NaturalGas,
    LiquefiedPetroleumGas,
    Oil,
    DistrictHeating,
}

/// <summary>
/// Site-to-source, carbon, and cost conversion factors.
/// </summary>
public static class EnergyConversionFactors
{
    public static double SiteToSource(EnergyCarrier carrier)
    {
        return carrier switch
        {
            EnergyCarrier.Electricity => 2.75d,
            EnergyCarrier.NaturalGas => 1.1d,
            EnergyCarrier.LiquefiedPetroleumGas => 1.1d,
            EnergyCarrier.Oil => 1.1d,
            EnergyCarrier.DistrictHeating => 0.728d,
            _ => throw new ArgumentOutOfRangeException(nameof(carrier), carrier, "Unknown energy carrier."),
        };
    }

    public static double SiteToCarbon(EnergyCarrier carrier)
    {
        return carrier switch
        {
            EnergyCarrier.Electricity => 0.4541d,
            EnergyCarrier.NaturalGas => 0.2024d,
            EnergyCarrier.LiquefiedPetroleumGas => 0.2326d,
            EnergyCarrier.Oil => 0.2603d,
            EnergyCarrier.DistrictHeating => 0.1358d,
            _ => throw new ArgumentOutOfRangeException(nameof(carrier), carrier, "Unknown energy carrier."),
        };
    }

    public static double SiteToCost(EnergyCarrier carrier)
    {
        return carrier switch
        {
            EnergyCarrier.Electricity => 162.92d,
            EnergyCarrier.NaturalGas => 78.12d,
            EnergyCarrier.LiquefiedPetroleumGas => 184.89d,
            EnergyCarrier.Oil => 141.92d,
            EnergyCarrier.DistrictHeating => 94.98d,
            _ => throw new ArgumentOutOfRangeException(nameof(carrier), carrier, "Unknown energy carrier."),
        };
    }
}

/// <summary>
/// Stable physical constants used while converting Korean usage profiles.
/// </summary>
public static class UsageProfileConstants
{
    public const double PeopleSensibleActivityWattsPerPerson = 70d;
    public const double DomesticHotWaterHeatWattHoursPerLitre = 40d;
}
