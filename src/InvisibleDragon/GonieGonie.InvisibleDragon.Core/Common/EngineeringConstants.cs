namespace GonieGonie.InvisibleDragon;

/// <summary>
/// Stable unit conversion factors used by InvisibleDragon engineering models.
/// </summary>
public static class UnitConversions
{
    public const double LitresToCubicMetres = 0.001d;

    public const double MillimetresToMetres = 0.001d;

    public const double FractionToPercent = 100d;

    public const double PercentToFraction = 0.01d;

    public const double WattsToKilowatts = 0.001d;
}

/// <summary>
/// Stable thermal assumptions emitted by the InvisibleDragon model writer.
/// </summary>
public static class ThermalDefaults
{
    public const double PeopleActivityLevelWattsPerPerson = 107d;
}
