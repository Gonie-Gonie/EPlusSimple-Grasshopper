using GonieGonie.BuildingEnergy.Contracts;
using DragonDaySchedule = GonieGonie.InvisibleDragon.Profile.DaySchedule;
using DragonProfile = GonieGonie.InvisibleDragon.Profile.Profile;
using DragonSchedule = GonieGonie.InvisibleDragon.Profile.Schedule;
using DragonScheduleType = GonieGonie.InvisibleDragon.Profile.ScheduleType;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class UsageProfileProductionSurfaceTests
{
    private static readonly string[] DictionaryKeys =
    {
        "name",
        "occupant_start",
        "occupant_end",
        "hvac_start",
        "hvac_end",
        "ventilation",
        "domestic_hotwater",
        "lighting_hours",
        "occupancy",
        "equipment",
        "heating_setpoint",
        "cooling_setpoint",
        "operate_weekdays",
        "vacations",
    };

    private static readonly string[] OperatingDayNames =
    {
        "monday",
        "wednesday",
        "holiday",
    };

    private static readonly UsageDay[] OperatingDays =
    {
        UsageDay.Monday,
        UsageDay.Wednesday,
        UsageDay.Holiday,
    };

    private static readonly string[] VacationKeys =
    {
        "start",
        "end",
    };

    [Fact]
    public void ToDictionaryPreservesExactOrderValuesAndNestedImmutability()
    {
        UsageProfile profile = CreateCustomProfile();
        EntityId sourceId = profile.Id;
        VacationPeriod[] sourceVacations = profile.Vacations.ToArray();

        OrderedMap<object> dictionary = profile.ToDictionary();

        Assert.Equal(DictionaryKeys, dictionary.Keys);
        Assert.False(dictionary.ContainsKey("id"));
        Assert.False(dictionary.ContainsKey("source"));
        Assert.DoesNotContain(
            typeof(IDictionary<string, object>),
            dictionary.GetType().GetInterfaces());
        Assert.Equal(profile.Name, Assert.IsType<string>(dictionary["name"]));
        Assert.Equal(22, Assert.IsType<int>(dictionary["occupant_start"]));
        Assert.Equal(6, Assert.IsType<int>(dictionary["occupant_end"]));
        Assert.Equal(21, Assert.IsType<int>(dictionary["hvac_start"]));
        Assert.Equal(7, Assert.IsType<int>(dictionary["hvac_end"]));
        Assert.Equal(1.5d, Assert.IsType<double>(dictionary["ventilation"]));
        Assert.Equal(64d, Assert.IsType<double>(dictionary["domestic_hotwater"]));
        Assert.Equal(4.5d, Assert.IsType<double>(dictionary["lighting_hours"]));
        Assert.Equal(560d, Assert.IsType<double>(dictionary["occupancy"]));
        Assert.Equal(32d, Assert.IsType<double>(dictionary["equipment"]));
        Assert.Equal(19d, Assert.IsType<double>(dictionary["heating_setpoint"]));
        Assert.Equal(27d, Assert.IsType<double>(dictionary["cooling_setpoint"]));

        IReadOnlyList<string> operatingDays =
            Assert.IsAssignableFrom<IReadOnlyList<string>>(dictionary["operate_weekdays"]);
        Assert.Equal(OperatingDayNames, operatingDays);
        IList<string> mutableOperatingDays = Assert.IsAssignableFrom<IList<string>>(operatingDays);
        Assert.True(mutableOperatingDays.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableOperatingDays.Add("friday"));

        IReadOnlyList<OrderedMap<object>> vacations =
            Assert.IsAssignableFrom<IReadOnlyList<OrderedMap<object>>>(dictionary["vacations"]);
        Assert.Collection(
            vacations,
            period => AssertVacation(period, "12/29", "01/03"),
            period => AssertVacation(period, "02/01", "02/14"));
        IList<OrderedMap<object>> mutableVacations =
            Assert.IsAssignableFrom<IList<OrderedMap<object>>>(vacations);
        Assert.True(mutableVacations.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableVacations.Add(vacations[0]));

        Assert.Same(sourceId, profile.Id);
        Assert.Equal(UsageProfileSource.Custom, profile.Source);
        Assert.Equal(OperatingDays, profile.OperatingDays);
        Assert.Equal(sourceVacations, profile.Vacations);
        Assert.Same(sourceVacations[0], profile.Vacations[0]);
        Assert.Same(sourceVacations[1], profile.Vacations[1]);
    }

    [Fact]
    public void UsageProfileSourceAppendsCustomWithoutChangingPersistedOrdinals()
    {
        Assert.Equal(0, (int)UsageProfileSource.Standard);
        Assert.Equal(1, (int)UsageProfileSource.Extended);
        Assert.Equal(2, (int)UsageProfileSource.Custom);

        UsageProfile defaultProfile = CreateDefaultProfile();
        DragonProfile converted = GreenRetrofitConverter.ConvertProfile(defaultProfile);
        Assert.Equal(UsageProfileSource.Standard, defaultProfile.Source);
        Assert.Equal(defaultProfile.Id, converted.Id);
        Assert.Equal("$FROM_DB$:default profile", converted.Name);
    }

    [Fact]
    public void StandaloneConversionMatchesFullConversionForStandardExtendedAndCustomProfiles()
    {
        UsageProfileDatabase database = SimpleDragonDatabase.Default.UsageProfiles;
        UsageProfile standard = Assert.Single(
            database.Items,
            profile => profile.Source == UsageProfileSource.Standard && profile.Name == "주거공간");
        UsageProfile extended = Assert.Single(
            database.Items,
            profile => profile.Source == UsageProfileSource.Extended && profile.Name == "교실(어린이집)");
        UsageProfile custom = CreateCustomProfile();
        var cases = new[]
        {
            (Profile: standard, Prefix: "$FROM_DB$:" + standard.Name),
            (Profile: extended, Prefix: "$FROM_DB$:" + extended.Name),
            (Profile: custom, Prefix: custom.Id.Value),
        };

        foreach ((UsageProfile profile, string prefix) in cases)
        {
            DragonProfile standalone = GreenRetrofitConverter.ConvertProfile(profile);
            DragonProfile fullModel = Assert.Single(ConvertThroughModel(profile));

            Assert.Equal(profile.Id, standalone.Id);
            Assert.Equal(prefix, standalone.Name);
            Assert.Equal(standalone, fullModel);
            AssertSevenScheduleTypes(standalone);
            Assert.All(Schedules(standalone), schedule => Assert.StartsWith(prefix + "-", schedule.Name));
            if (profile.Source == UsageProfileSource.Custom)
            {
                AssertCustomScheduleSemantics(standalone);
            }
        }
    }

    [Fact]
    public void StandaloneConversionIsFreshNullSafeAndDoesNotMutateSource()
    {
        UsageProfile source = CreateCustomProfile();
        UsageDay[] originalDays = source.OperatingDays.ToArray();
        VacationPeriod[] originalVacations = source.Vacations.ToArray();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => GreenRetrofitConverter.ConvertProfile(null!));
        Assert.Equal("profile", exception.ParamName);

        DragonProfile first = GreenRetrofitConverter.ConvertProfile(source);
        DragonProfile second = GreenRetrofitConverter.ConvertProfile(source);

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        foreach ((DragonSchedule left, DragonSchedule right) in Schedules(first).Zip(Schedules(second)))
        {
            Assert.NotSame(left, right);
            Assert.Equal(left, right);
        }

        Assert.Equal("PROFILE-CUSTOM", source.Id.Value);
        Assert.Equal("custom profile", source.Name);
        Assert.Equal(UsageProfileSource.Custom, source.Source);
        Assert.Equal(originalDays, source.OperatingDays);
        Assert.Equal(originalVacations, source.Vacations);
        Assert.Same(originalVacations[0], source.Vacations[0]);
        Assert.Same(originalVacations[1], source.Vacations[1]);
    }

    [Fact]
    public void FullModelConversionStillAliasesOneCachedProfileAcrossZones()
    {
        UsageProfile source = CreateCustomProfile();

        DragonProfile[] converted = ConvertThroughModel(source, zoneCount: 2);
        DragonProfile standalone = GreenRetrofitConverter.ConvertProfile(source);

        Assert.Equal(2, converted.Length);
        Assert.Same(converted[0], converted[1]);
        Assert.Same(converted[0].HeatingSetpoint, converted[1].HeatingSetpoint);
        Assert.Same(converted[0].HotWater, converted[1].HotWater);
        Assert.NotSame(converted[0], standalone);
        Assert.Equal(converted[0], standalone);
    }

    [Fact]
    public void VacationConversionKeepsReversedWindowAsNoOpAndRejectsFebruary29()
    {
        VacationPeriod reversed = new(new MonthDay(12, 29), new MonthDay(1, 3));
        UsageProfile wrapped = CreateCustomProfile(new[] { reversed });
        DragonProfile converted = GreenRetrofitConverter.ConvertProfile(wrapped);
        DragonProfile baseline = GreenRetrofitConverter.ConvertProfile(
            CreateCustomProfile(Array.Empty<VacationPeriod>()));
        foreach ((DragonSchedule expected, DragonSchedule actual) in
            Schedules(baseline).Zip(Schedules(converted)))
        {
            Assert.NotSame(expected, actual);
            Assert.Equal(expected, actual);
        }

        Assert.Single(wrapped.Vacations);
        Assert.Same(reversed, wrapped.Vacations[0]);

        UsageProfile leapDay = CreateCustomProfile(
            new[] { new VacationPeriod(new MonthDay(2, 29), new MonthDay(3, 1)) });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GreenRetrofitConverter.ConvertProfile(leapDay));
        Assert.Equal("PROFILE-CUSTOM", leapDay.Id.Value);
        Assert.Single(leapDay.Vacations);
        Assert.Equal(new MonthDay(2, 29), leapDay.Vacations[0].Start);
    }

    private static void AssertVacation(OrderedMap<object> period, string start, string end)
    {
        Assert.Equal(VacationKeys, period.Keys);
        Assert.DoesNotContain(
            typeof(IDictionary<string, object>),
            period.GetType().GetInterfaces());
        Assert.Equal(start, Assert.IsType<string>(period["start"]));
        Assert.Equal(end, Assert.IsType<string>(period["end"]));
    }

    private static void AssertSevenScheduleTypes(DragonProfile profile)
    {
        Assert.Equal(DragonScheduleType.Temperature, Assert.IsType<DragonSchedule>(profile.HeatingSetpoint).Type);
        Assert.Equal(DragonScheduleType.Temperature, Assert.IsType<DragonSchedule>(profile.CoolingSetpoint).Type);
        Assert.Equal(DragonScheduleType.OnOff, Assert.IsType<DragonSchedule>(profile.HvacAvailability).Type);
        Assert.Equal(DragonScheduleType.Real, Assert.IsType<DragonSchedule>(profile.Occupant).Type);
        Assert.Equal(DragonScheduleType.Fraction, Assert.IsType<DragonSchedule>(profile.Lighting).Type);
        Assert.Equal(DragonScheduleType.Real, Assert.IsType<DragonSchedule>(profile.Equipment).Type);
        Assert.Equal(DragonScheduleType.Real, Assert.IsType<DragonSchedule>(profile.HotWater).Type);
    }

    private static void AssertCustomScheduleSemantics(DragonProfile profile)
    {
        DateTime operatingMonday = new(2026, 2, 16);
        DateTime nonOperatingTuesday = new(2026, 2, 17);
        DateTime ordinaryVacationMonday = new(2026, 2, 2);
        DateTime wrappedVacationWednesday = new(2026, 12, 30);
        int hour23 = 23 * DragonDaySchedule.IntervalsPerHour;
        int noon = 12 * DragonDaySchedule.IntervalsPerHour;

        Assert.Equal(DayOfWeek.Monday, operatingMonday.DayOfWeek);
        Assert.Equal(19d, Day(profile.HeatingSetpoint!, operatingMonday)[noon]);
        Assert.Equal(27d, Day(profile.CoolingSetpoint!, operatingMonday)[noon]);
        Assert.Equal(1d, Day(profile.HvacAvailability!, operatingMonday)[hour23]);
        Assert.Equal(1d, Day(profile.Occupant!, operatingMonday)[hour23]);
        Assert.Equal(0d, Day(profile.Occupant!, operatingMonday)[noon]);
        Assert.Equal(4d, Day(profile.Equipment!, operatingMonday)[hour23]);
        Assert.Equal(0.2d, Day(profile.HotWater!, operatingMonday)[hour23], 12);
        Assert.Equal(4.5d, Day(profile.Lighting!, operatingMonday).IntegralHours, 12);

        Assert.Equal(0d, Day(profile.Occupant!, nonOperatingTuesday)[hour23]);
        Assert.Equal(0d, Day(profile.Occupant!, ordinaryVacationMonday)[hour23]);
        Assert.Equal(0d, Day(profile.HotWater!, ordinaryVacationMonday)[hour23]);
        Assert.Equal(0d, Day(profile.Lighting!, ordinaryVacationMonday).IntegralHours);
        Assert.Equal(1d, Day(profile.Occupant!, wrappedVacationWednesday)[hour23]);
    }

    private static DragonDaySchedule Day(DragonSchedule schedule, DateTime date)
    {
        return schedule[date].GetDaySchedule(date.DayOfWeek);
    }

    private static DragonSchedule[] Schedules(DragonProfile profile)
    {
        return new[]
        {
            Assert.IsType<DragonSchedule>(profile.HeatingSetpoint),
            Assert.IsType<DragonSchedule>(profile.CoolingSetpoint),
            Assert.IsType<DragonSchedule>(profile.HvacAvailability),
            Assert.IsType<DragonSchedule>(profile.Occupant),
            Assert.IsType<DragonSchedule>(profile.Lighting),
            Assert.IsType<DragonSchedule>(profile.Equipment),
            Assert.IsType<DragonSchedule>(profile.HotWater),
        };
    }

    private static UsageProfile CreateCustomProfile(
        IEnumerable<VacationPeriod>? vacations = null)
    {
        var operation = Enum.GetValues<UsageDay>()
            .ToDictionary(
                day => day,
                day => day is UsageDay.Monday or UsageDay.Wednesday or UsageDay.Holiday);
        return new UsageProfile(
            "custom profile",
            22,
            6,
            21,
            7,
            1.5d,
            64d,
            4.5d,
            560d,
            32d,
            19d,
            27d,
            operation,
            vacations ?? new[]
            {
                new VacationPeriod(new MonthDay(12, 29), new MonthDay(1, 3)),
                new VacationPeriod(new MonthDay(2, 1), new MonthDay(2, 14)),
            },
            UsageProfileSource.Custom,
            new EntityId("PROFILE-CUSTOM"));
    }

    private static UsageProfile CreateDefaultProfile()
    {
        var operation = Enum.GetValues<UsageDay>()
            .ToDictionary(day => day, _ => true);
        return new UsageProfile(
            "default profile",
            8,
            18,
            7,
            19,
            1d,
            40d,
            8d,
            700d,
            40d,
            20d,
            26d,
            operation);
    }

    private static DragonProfile[] ConvertThroughModel(
        UsageProfile profile,
        int zoneCount = 1)
    {
        Zone[] zones = Enumerable.Range(1, zoneCount)
            .Select(index => new Zone(
                "source zone " + index,
                1,
                3d,
                Array.Empty<Surface>(),
                profile.Name,
                profile,
                0d,
                id: new EntityId("SOURCE-ZONE-" + index)))
            .ToArray();
        var weather = new WeatherSelection(
            new WeatherMetadata(
                "test district",
                "1111111111",
                "Suburbs",
                37.5d,
                127d,
                "test station",
                "TMY",
                37.5d,
                127d,
                "test.epw"),
            "test climate",
            new DateTime(2020, 1, 1));
        var model = new GreenRetrofitModel(
            "profile-only model",
            0d,
            "test address",
            new DateTime(2020, 1, 1),
            false,
            new[] { new BuildingFloor(1, zones) },
            Array.Empty<Material>(),
            Array.Empty<SurfaceConstruction>(),
            Array.Empty<FenestrationConstruction>(),
            weather: weather);
        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(
            model,
            new GreenRetrofitConversionOptions
            {
                IncludeModelValidationDiagnostics = false,
            });

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        return result.RequireEnergyModel().Zones.Select(zone => zone.Profile).ToArray();
    }
}
