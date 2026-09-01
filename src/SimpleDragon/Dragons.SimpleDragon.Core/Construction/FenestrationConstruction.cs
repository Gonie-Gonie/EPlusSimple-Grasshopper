using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon;

/// <summary>
/// A window or door construction represented by U-value and optional SHGC.
/// </summary>
public sealed class FenestrationConstruction
{
    public FenestrationConstruction(
        string name,
        double uValue,
        double? solarHeatGainCoefficient = null,
        EntityId? id = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        UValue = DomainSupport.FinitePositive(uValue, nameof(uValue));
        if (solarHeatGainCoefficient.HasValue
            && (double.IsNaN(solarHeatGainCoefficient.Value)
                || double.IsInfinity(solarHeatGainCoefficient.Value)
                || solarHeatGainCoefficient.Value <= 0d
                || solarHeatGainCoefficient.Value >= 1d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(solarHeatGainCoefficient),
                solarHeatGainCoefficient,
                "SHGC must be strictly between zero and one when supplied.");
        }

        SolarHeatGainCoefficient = solarHeatGainCoefficient;
        Id = id ?? DeterministicDomainId.Create("CTFN", Name, UValue, SolarHeatGainCoefficient);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public double UValue { get; }

    public double? SolarHeatGainCoefficient { get; }

    public bool IsTransparent => SolarHeatGainCoefficient.HasValue && SolarHeatGainCoefficient.Value > 0d;
}

/// <summary>
/// Exact six-column key used by the Korean fenestration table.
/// </summary>
public sealed class FenestrationConstructionKey : IEquatable<FenestrationConstructionKey>
{
    public FenestrationConstructionKey(
        string windowCount,
        string lowEGlass,
        string argon,
        string thermalBreak,
        string frame,
        string cavity)
    {
        WindowCount = DomainSupport.RequiredText(windowCount, nameof(windowCount));
        LowEGlass = DomainSupport.RequiredText(lowEGlass, nameof(lowEGlass));
        Argon = DomainSupport.RequiredText(argon, nameof(argon));
        ThermalBreak = DomainSupport.RequiredText(thermalBreak, nameof(thermalBreak));
        Frame = DomainSupport.RequiredText(frame, nameof(frame));
        Cavity = DomainSupport.RequiredText(cavity, nameof(cavity));
    }

    public string WindowCount { get; }

    public string LowEGlass { get; }

    public string Argon { get; }

    public string ThermalBreak { get; }

    public string Frame { get; }

    public string Cavity { get; }

    public bool Equals(FenestrationConstructionKey? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(WindowCount, other.WindowCount)
            && StringComparer.Ordinal.Equals(LowEGlass, other.LowEGlass)
            && StringComparer.Ordinal.Equals(Argon, other.Argon)
            && StringComparer.Ordinal.Equals(ThermalBreak, other.ThermalBreak)
            && StringComparer.Ordinal.Equals(Frame, other.Frame)
            && StringComparer.Ordinal.Equals(Cavity, other.Cavity);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as FenestrationConstructionKey);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(WindowCount);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(LowEGlass);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Argon);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ThermalBreak);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Frame);
            return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Cavity);
        }
    }

    public override string ToString()
    {
        return WindowCount + "&" + LowEGlass + "&" + Argon + "&" + ThermalBreak + "&" + Frame + "&" + Cavity;
    }
}
