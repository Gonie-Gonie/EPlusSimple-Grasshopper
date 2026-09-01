using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Shape;

public interface IShadingDevice
{
    string Name { get; }
}

public sealed record Blind : IShadingDevice
{
    public Blind(
        string name,
        double slatWidthMetres,
        double slatSeparationMetres,
        double slatAngleDegrees,
        double frontReflectance,
        double backReflectance)
    {
        Name = DomainGuard.RequiredText(name, nameof(name));
        SlatWidthMetres = DomainGuard.Positive(slatWidthMetres, nameof(slatWidthMetres));
        SlatSeparationMetres = DomainGuard.Positive(slatSeparationMetres, nameof(slatSeparationMetres));
        SlatAngleDegrees = DomainGuard.InRange(slatAngleDegrees, -180, 180, nameof(slatAngleDegrees));
        FrontReflectance = DomainGuard.InRange(frontReflectance, 0, 1, nameof(frontReflectance));
        BackReflectance = DomainGuard.InRange(backReflectance, 0, 1, nameof(backReflectance));
    }

    public string Name { get; }

    public double SlatWidthMetres { get; }

    public double SlatSeparationMetres { get; }

    public double SlatAngleDegrees { get; }

    public double FrontReflectance { get; }

    public double BackReflectance { get; }
}

public sealed record Shade : IShadingDevice
{
    public Shade(string name, double transmittance, double reflectance)
    {
        Name = DomainGuard.RequiredText(name, nameof(name));
        Transmittance = DomainGuard.InRange(transmittance, 0, 1, nameof(transmittance));
        Reflectance = DomainGuard.InRange(reflectance, 0, 1, nameof(reflectance));
        if (Transmittance + Reflectance > 1)
        {
            throw new ArgumentException("Shade transmittance and reflectance cannot sum to more than one.");
        }
    }

    public string Name { get; }

    public double Transmittance { get; }

    public double Reflectance { get; }

    public double Emissivity => 1 - Transmittance - Reflectance;
}
