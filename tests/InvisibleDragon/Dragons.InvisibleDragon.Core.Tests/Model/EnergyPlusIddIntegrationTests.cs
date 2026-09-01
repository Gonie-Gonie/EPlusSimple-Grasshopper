using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Shape;

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class EnergyPlusIddIntegrationTests
{
    [Fact]
    public void RepresentativeModelPassesInstalledEnergyPlusIddValidation()
    {
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("ENERGYPLUS_ROOT") ?? @"C:\EnergyPlusV24-2-0",
            "Energy+.idd");
        if (!File.Exists(path))
        {
            return;
        }

        IddSchema schema = IddParser.ParseFile(path);
        IdfDocument document = EnergyModelFixtureMatrixTests.CreateRepresentativeModel().ToIdfDocument(schema);

        ValidationResult result = IdfValidator.Validate(
            document,
            new IdfValidationOptions { ValidateSchemaDefaults = false });

        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
    }

    [Fact]
    public void ErvAndPvFamiliesPassInstalledEnergyPlusIddValidation()
    {
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("ENERGYPLUS_ROOT") ?? @"C:\EnergyPlusV24-2-0",
            "Energy+.idd");
        if (!File.Exists(path))
        {
            return;
        }

        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("ZONE-IDD-ERV", "IDD ERV");
        var erv = new EnergyRecoveryVentilator(new EntityId("ERV-IDD"), "IDD ERV", 0.75, 0.65, 0.2);
        var pv = new PhotovoltaicPanel(new EntityId("PV-IDD"), "IDD PV", 10, 30, 180, 0.2);
        var model = new EnergyModel(
            "IDD ERV PV",
            new[] { zone },
            ventilationAssignments: new[] { new ZoneVentilationAssignment(zone.Id, erv) },
            photovoltaicPanels: new[] { pv });
        IddSchema schema = IddParser.ParseFile(path);

        ValidationResult result = IdfValidator.Validate(
            model.ToIdfDocument(schema),
            new IdfValidationOptions { ValidateSchemaDefaults = false });

        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
    }

    [Fact]
    public void DistrictAndElectricRadiantFamiliesPassInstalledEnergyPlusIddValidation()
    {
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("ENERGYPLUS_ROOT") ?? @"C:\EnergyPlusV24-2-0",
            "Energy+.idd");
        if (!File.Exists(path))
        {
            return;
        }

        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("ZONE-IDD-RAD", "IDD Radiant");
        var district = new DistrictHeating(new EntityId("DISTRICT-IDD"), "IDD District");
        var hydronic = new RadiantFloor(new EntityId("RAD-IDD"), "IDD Hydronic", district);
        var electric = new ElectricRadiantFloor(new EntityId("ERAD-IDD"), "IDD Electric");
        var model = new EnergyModel(
            "IDD Radiant Families",
            new[] { zone },
            new[]
            {
                new ZoneHvacAssignment(
                    zone.Id,
                    new SupplyGroup(new SupplySystem[] { hydronic, electric })),
            });
        IddSchema schema = IddParser.ParseFile(path);

        ValidationResult result = IdfValidator.Validate(
            model.ToIdfDocument(schema),
            new IdfValidationOptions { ValidateSchemaDefaults = false });

        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
    }
}
