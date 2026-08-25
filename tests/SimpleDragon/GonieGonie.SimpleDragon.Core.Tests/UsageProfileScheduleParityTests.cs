using System.Globalization;
using System.Text.Json;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using DragonSchedule = GonieGonie.InvisibleDragon.Profile.Schedule;
using DragonZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;
using SimpleZone = GonieGonie.SimpleDragon.Zone;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class UsageProfileScheduleParityTests
{
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static IEnumerable<object[]> ProfileIndexes()
    {
        return Enumerable.Range(0, 24).Select(index => new object[] { index });
    }

    [Fact]
    public void OracleCoversEveryPinnedProfileInDatabaseOrder()
    {
        UsageProfileOracle oracle = ReadOracle();
        IReadOnlyList<UsageProfile> profiles = SimpleDragonDatabase.Default.UsageProfiles.Items;

        Assert.Equal("goniegonie.simpledragon.usage-profile-schedule-oracle.v1", oracle.Schema);
        Assert.Equal(UpstreamCommit, oracle.UpstreamCommit);
        Assert.Equal(24, oracle.ProfileCount);
        Assert.Equal(oracle.ProfileCount, oracle.Profiles.Count);
        Assert.Equal(oracle.ProfileCount, profiles.Count);
        Assert.Equal(oracle.Profiles.Select(item => item.Name), profiles.Select(item => item.Name));
    }

    [Theory]
    [MemberData(nameof(ProfileIndexes))]
    public void LegacyConversionMatchesPinnedPythonScheduleOracle(int profileIndex)
    {
        UsageProfileOracle root = ReadOracle();
        UsageProfileExpectation expected = root.Profiles[profileIndex];
        UsageProfile profile = SimpleDragonDatabase.Default.UsageProfiles.Items[profileIndex];

        AssertProfileMetadata(expected, profile);
        GreenRetrofitConversionResult conversion = ConvertProfile(profile);
        EnergyModel model = conversion.RequireEnergyModel();
        GonieGonie.InvisibleDragon.Shape.Zone zone = Assert.Single(model.Zones);
        DragonZoneProfile actualProfile = zone.Profile;
        IdfDocument idf = conversion.ToIdfDocument();

        var schedules = new Dictionary<string, DragonSchedule>
        {
            ["heating_setpoint"] = actualProfile.HeatingSetpoint!,
            ["cooling_setpoint"] = actualProfile.CoolingSetpoint!,
            ["hvac_availability"] = actualProfile.HvacAvailability!,
            ["occupant"] = actualProfile.Occupant!,
            ["lighting"] = actualProfile.Lighting!,
            ["equipment"] = actualProfile.Equipment!,
        };

        foreach ((string purpose, DragonSchedule schedule) in schedules)
        {
            ScheduleExpectation scheduleOracle = expected.Schedules[purpose];
            AssertScheduleValues(expected.Name, purpose, scheduleOracle, schedule);
            IdfObject actualObject = Assert.Single(
                idf["Schedule:Compact"],
                item => StringComparer.Ordinal.Equals(item.Name, schedule.Name));
            AssertIdfFields(expected.Name, purpose, scheduleOracle.IdfFields, actualObject);
        }

        Assert.Equal(expected.OccupancyDensity, actualProfile.Occupant!.Maximum);
        Assert.Equal(expected.EquipmentPowerDensity, actualProfile.Equipment!.Maximum);
        AssertNormalizedLoad(
            expected,
            "occupant",
            zone.Name,
            actualProfile.Occupant,
            idf);
        AssertNormalizedLoad(
            expected,
            "equipment",
            zone.Name,
            actualProfile.Equipment,
            idf);

        IdfObject people = Assert.Single(idf["People"]);
        Assert.Equal(
            expected.OccupancyDensity.ToString("R", CultureInfo.InvariantCulture),
            people[5]);
        IdfObject equipment = Assert.Single(idf["ElectricEquipment"]);
        Assert.Equal(
            expected.EquipmentPowerDensity.ToString("R", CultureInfo.InvariantCulture),
            equipment[5]);
    }

    private static void AssertProfileMetadata(
        UsageProfileExpectation expected,
        UsageProfile actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Id, "$FROM_DB$:" + actual.Name);
        Assert.Equal(expected.Source, actual.Source.ToString().ToLowerInvariant());
        Assert.Equal(expected.OccupiedHours, actual.OccupiedHours);
        Assert.Equal(
            expected.OperatingDays,
            actual.OperatingDays.Select(day => day.ToString().ToLowerInvariant()));
        Assert.Equal(
            expected.Vacations.Select(item => (item.Start, item.End)),
            actual.Vacations.Select(item => (item.Start.ToString(), item.End.ToString())));
    }

    private static void AssertScheduleValues(
        string profileName,
        string purpose,
        ScheduleExpectation expected,
        DragonSchedule actual)
    {
        Assert.True(
            StringComparer.Ordinal.Equals(expected.Name, actual.Name),
            profileName + " / " + purpose + " schedule name differs.\nExpected: "
                + expected.Name + "\nActual:   " + actual.Name);
        Assert.Equal(expected.Type, actual.Type.ToString().ToLowerInvariant());
        Assert.Equal(expected.Maximum, actual.Maximum);
    }

    private static void AssertNormalizedLoad(
        UsageProfileExpectation profile,
        string purpose,
        string zoneName,
        DragonSchedule source,
        IdfDocument idf)
    {
        ScheduleExpectation expected = profile.Schedules["normalized_" + purpose];
        string actualName = source.Name + "_normalized:for:" + zoneName + ":" + purpose;
        IdfObject actual = Assert.Single(
            idf["Schedule:Compact"],
            item => StringComparer.Ordinal.Equals(item.Name, actualName));
        AssertIdfFields(profile.Name, "normalized_" + purpose, expected.IdfFields, actual);
    }

    private static void AssertIdfFields(
        string profileName,
        string purpose,
        IReadOnlyList<string> expected,
        IdfObject actual)
    {
        string[] actualFields = actual.Fields.Select(field => field.Value).ToArray();
        Assert.True(
            expected.SequenceEqual(actualFields, StringComparer.Ordinal),
            profileName + " / " + purpose + " Schedule:Compact fields differ.\nExpected:\n"
                + string.Join("\n", expected.Select((value, index) => index + ": " + value))
                + "\nActual:\n"
                + string.Join("\n", actualFields.Select((value, index) => index + ": " + value)));
    }

    private static GreenRetrofitConversionResult ConvertProfile(UsageProfile profile)
    {
        GreenRetrofitModel template = GrmReader.ReadFile(SimpleDragonFixture()).RequireModel();
        SimpleZone sourceZone = Assert.Single(template.Zones);
        var zone = new SimpleZone(
            sourceZone.Name,
            sourceZone.FloorNumber,
            sourceZone.Height,
            sourceZone.Surfaces,
            profile.Name,
            profile,
            sourceZone.LightDensity,
            id: sourceZone.Id);
        var model = new GreenRetrofitModel(
            template.Name,
            template.NorthAxis,
            template.Address,
            template.Vintage,
            template.IsMultifamilyHousing,
            new[] { new BuildingFloor(zone.FloorNumber, new[] { zone }) },
            template.Materials,
            template.SurfaceConstructions,
            template.FenestrationConstructions,
            weather: template.Weather);

        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(model);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return result;
    }

    private static UsageProfileOracle ReadOracle()
    {
        string path = Path.Combine(
            FindRepositoryRoot().FullName,
            "fixtures",
            "reference",
            "python-0.7.0",
            "usage-profile-schedule-oracle.json");
        return JsonSerializer.Deserialize<UsageProfileOracle>(
            File.ReadAllText(path),
            SerializerOptions)
            ?? throw new InvalidDataException("Could not deserialize UsageProfile schedule oracle: " + path);
    }

    private static string SimpleDragonFixture()
    {
        return Path.Combine(
            FindRepositoryRoot().FullName,
            "fixtures",
            "simple-dragon",
            "grm",
            "ASHRAE 140 modified.grm");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "upstream", "upstream.lock.json")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root from the test output directory.");
    }

    private sealed class UsageProfileOracle
    {
        public string Schema { get; init; } = string.Empty;

        public string UpstreamCommit { get; init; } = string.Empty;

        public int ProfileCount { get; init; }

        public List<UsageProfileExpectation> Profiles { get; init; } = new();
    }

    private sealed class UsageProfileExpectation
    {
        public string Name { get; init; } = string.Empty;

        public string Id { get; init; } = string.Empty;

        public string Source { get; init; } = string.Empty;

        public double OccupiedHours { get; init; }

        public List<string> OperatingDays { get; init; } = new();

        public List<VacationExpectation> Vacations { get; init; } = new();

        public double OccupancyDensity { get; init; }

        public double EquipmentPowerDensity { get; init; }

        public Dictionary<string, ScheduleExpectation> Schedules { get; init; } = new();
    }

    private sealed class VacationExpectation
    {
        public string Start { get; init; } = string.Empty;

        public string End { get; init; } = string.Empty;
    }

    private sealed class ScheduleExpectation
    {
        public string Name { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        public double Minimum { get; init; }

        public double Maximum { get; init; }

        public List<string> IdfFields { get; init; } = new();
    }
}
