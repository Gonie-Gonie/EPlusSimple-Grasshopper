using System.Globalization;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using DragonConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using DragonSurface = GonieGonie.InvisibleDragon.Shape.Surface;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class GreenRetrofitConversionTests
{
    [Fact]
    public void AshraeFixtureConvertsToValidEnergyModelAndEnergyPlusReadyIdf()
    {
        GreenRetrofitModel source = GrmReader.ReadFile(Fixture("grm")).RequireModel();

        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(source);

        Assert.True(result.Success, Describe(result));
        EnergyModel model = result.RequireEnergyModel();
        Assert.Equal("ASHRAE 140 modified", model.Name);
        Assert.Equal(Terrain.Suburbs, model.Terrain);
        Assert.Single(model.Zones);
        Assert.Equal(6, model.Surfaces.Count);
        Assert.Equal(2, model.Surfaces.Sum(surface => surface.Openings.Count));
        Assert.Equal(6, result.SurfaceConversions.Count);
        Assert.All(result.SurfaceConversions, item =>
        {
            Assert.Equal(item.SourceSurfaceId, item.ConvertedSurfaceId);
            Assert.False(item.IsSynthesizedCounterpart);
        });
        Assert.Equal(48d, model.Zones[0].FloorArea, 8);
        Assert.Equal(0.105d, model.Zones[0].InfiltrationAirChangesPerHour, 8);
        Assert.Equal(4d * 48d / 3600d, model.Zones[0].OutdoorAirFlowCubicMetresPerSecond, 8);

        Assert.Single(model.HvacAssignments);
        SupplyGroup group = model.HvacAssignments[0].Supply;
        Assert.Equal(2, group.Systems.Count);
        Assert.Contains(group.Systems, item => item is AirHandlingUnit);
        Assert.Contains(group.Systems, item => item is RadiantFloor);
        Assert.Empty(model.VentilationAssignments);
        Assert.Empty(model.PhotovoltaicPanels);
        Assert.True(model.Validate().IsValid, Describe(model.Validate().Diagnostics));

        IdfDocument idf = result.ToIdfDocument();
        Assert.Equal("24.2", idf.EnergyPlusVersion);
        Assert.Single(idf["Building"]);
        Assert.Single(idf["Zone"]);
        Assert.Equal(6, idf["BuildingSurface:Detailed"].Count);
        Assert.Equal(2, idf["FenestrationSurface:Detailed"].Count);
        Assert.Single(idf["AirConditioner:VariableRefrigerantFlow"]);
        Assert.Single(idf["Boiler:HotWater"]);
        Assert.Single(idf["ZoneHVAC:LowTemperatureRadiant:VariableFlow"]);

        IdfDocument oracle = IdfParser.ParseFile(ReferenceIdf());
        Assert.Equal(oracle["Building"][0][0], idf["Building"][0][0]);
        Assert.Equal(oracle["Building"][0][2], idf["Building"][0][2]);
        Assert.Equal(
            oracle["Zone"].Select(item => item.Name),
            idf["Zone"].Select(item => item.Name));
        Assert.Equal(
            oracle["BuildingSurface:Detailed"].Select(item => (item.Name, item[1])),
            idf["BuildingSurface:Detailed"].Select(item => (item.Name, item[1])));
        Assert.Equal(
            oracle["Window"].Select(item => item.Name),
            idf["FenestrationSurface:Detailed"].Select(item => item.Name));
        Assert.Equal(oracle["Boiler:HotWater"][0][0], idf["Boiler:HotWater"][0][0]);
        Assert.Equal(oracle["Boiler:HotWater"][0][1], idf["Boiler:HotWater"][0][1]);

        string first = IdfWriter.Write(idf);
        string second = IdfWriter.Write(GreenRetrofitConverter.ToIdfDocument(source));
        Assert.Equal(first, second);
        Assert.Contains("SURF-0x000003", first, StringComparison.Ordinal);
        Assert.Contains("FNST-0x000000", first, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertedOpeningsRetainAreaAndDoNotOverlap()
    {
        GreenRetrofitModel source = GrmReader.ReadFile(Fixture("grm")).RequireModel();
        EnergyModel converted = GreenRetrofitConverter.Convert(source).RequireEnergyModel();
        DragonSurface wall = Assert.Single(converted.Surfaces, surface => surface.Openings.Count == 2);

        Assert.Equal(21.6d, wall.GrossArea, 8);
        Assert.All(wall.Openings, opening => Assert.Equal(6d, opening.Polygon.Area, 8));
        Assert.False(wall.Openings[0].Polygon.IntersectsInterior(wall.Openings[1].Polygon));
        Assert.True(wall.Validate().IsValid, Describe(wall.Validate().Diagnostics));
    }

    [Fact]
    public void ConvertedIdfKeepsFenestrationVerticesAndPeopleActivityInTheirDeclaredFields()
    {
        GreenRetrofitModel source = GrmReader.ReadFile(Fixture("grm")).RequireModel();

        IdfDocument idf = GreenRetrofitConverter.ToIdfDocument(source);

        Assert.All(idf["FenestrationSurface:Detailed"], fenestration =>
        {
            Assert.Equal(21, fenestration.Count);
            Assert.Equal("1", fenestration[7]);
            Assert.Equal("4", fenestration[8]);
            Assert.Equal(12, fenestration.Fields.Skip(9).Count());
        });
        IdfObject people = Assert.Single(idf["People"]);
        Assert.Equal(10, people.Count);
        Assert.Equal("People/Area", people[3]);
        Assert.Equal("$DEFAULT$PEOPLEACTIVITY", people[9]);
        IdfObject equipment = Assert.Single(idf["ZoneHVAC:EquipmentList"]);
        Assert.Equal("1", equipment[4]);
        Assert.Equal("1", equipment[5]);
        Assert.Equal("2", equipment[10]);
        Assert.Equal("2", equipment[11]);
        IdfObject noMass = Assert.Single(
            idf["Material:NoMass"],
            material => material.Name!.StartsWith("MTRL-0x000004_", StringComparison.Ordinal));
        Assert.Equal(
            1.003d / 0.04d,
            double.Parse(noMass[2], NumberStyles.Float, CultureInfo.InvariantCulture),
            12);
        Assert.DoesNotContain(
            idf["Material"],
            material => material.Name!.StartsWith("MTRL-0x000004_", StringComparison.Ordinal));
    }

    [Fact]
    public void StandardProfileDisplayNameMayContainWhitespace()
    {
        GreenRetrofitModel source = GrmReader.ReadFile(Fixture("grm")).RequireModel();
        Zone originalZone = Assert.Single(source.Zones);
        UsageProfile originalProfile = Assert.IsType<UsageProfile>(originalZone.Profile);
        var operation = Enum.GetValues<UsageDay>()
            .ToDictionary(day => day, originalProfile.OperatesOn);
        var spacedProfile = new UsageProfile(
            "Office profile with spaces",
            originalProfile.OccupantStart,
            originalProfile.OccupantEnd,
            originalProfile.HvacStart,
            originalProfile.HvacEnd,
            originalProfile.Ventilation,
            originalProfile.DomesticHotWater,
            originalProfile.LightingHours,
            originalProfile.Occupancy,
            originalProfile.Equipment,
            originalProfile.HeatingSetpoint,
            originalProfile.CoolingSetpoint,
            operation,
            originalProfile.Vacations,
            UsageProfileSource.Standard);
        var zone = new Zone(
            originalZone.Name,
            originalZone.FloorNumber,
            originalZone.Height,
            originalZone.Surfaces,
            spacedProfile.Name,
            spacedProfile,
            originalZone.LightDensity,
            id: originalZone.Id);
        var model = new GreenRetrofitModel(
            source.Name,
            source.NorthAxis,
            source.Address,
            source.Vintage,
            source.IsMultifamilyHousing,
            new[] { new BuildingFloor(zone.FloorNumber, new[] { zone }) },
            source.Materials,
            source.SurfaceConstructions,
            source.FenestrationConstructions,
            weather: source.Weather);

        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(model);

        Assert.True(result.Success, Describe(result));
        Assert.Equal(spacedProfile.Id, Assert.Single(result.RequireEnergyModel().Zones).Profile.Id);
        Assert.Equal("$FROM_DB$:Office profile with spaces",
            Assert.Single(result.RequireEnergyModel().Zones).Profile.Name);
    }

    [Fact]
    public void ReciprocalZoneSurfacesNormalizeToExactlyOneMirroredPair()
    {
        GreenRetrofitModel template = GrmReader.ReadFile(Fixture("grm")).RequireModel();
        SurfaceConstruction construction = template.SurfaceConstructions[0];
        FenestrationConstruction glazing = template.FenestrationConstructions
            .First(item => item.IsTransparent);
        var openingA = new Fenestration(
            "opening A",
            FenestrationType.Window,
            2d,
            glazing.Id.Value,
            glazing,
            BlindType.Shade,
            new GonieGonie.BuildingEnergy.Contracts.EntityId("OPEN-A"));
        var openingB = new Fenestration(
            "opening B",
            FenestrationType.Window,
            2d,
            glazing.Id.Value,
            glazing,
            BlindType.Shade,
            new GonieGonie.BuildingEnergy.Contracts.EntityId("OPEN-B"));
        Surface surfaceA = ZoneBoundary(
            "SURFACE-A",
            "ZONE-B",
            12d,
            construction,
            new[] { openingA });
        Surface surfaceB = ZoneBoundary(
            "SURFACE-B",
            "ZONE-A",
            12d,
            construction,
            new[] { openingB });
        GreenRetrofitModel source = AdjacencyModel(template, new[] { surfaceA }, new[] { surfaceB });

        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(source);

        Assert.True(result.Success, Describe(result));
        EnergyModel converted = result.RequireEnergyModel();
        Assert.Equal(2, converted.Surfaces.Count);
        Assert.DoesNotContain(converted.Surfaces, item =>
            item.Id.Value.StartsWith("CLONE:", StringComparison.Ordinal));
        DragonSurface convertedA = Assert.Single(converted.Surfaces, item => item.Id.Equals(surfaceA.Id));
        DragonSurface convertedB = Assert.Single(converted.Surfaces, item => item.Id.Equals(surfaceB.Id));
        Assert.Equal(convertedB.Id, convertedA.Boundary.AdjacentSurfaceId);
        Assert.Equal(convertedA.Id, convertedB.Boundary.AdjacentSurfaceId);
        Assert.True(convertedA.Polygon.IsGeometricallyEquivalentTo(
            convertedB.Polygon,
            allowReversedWinding: true));
        Assert.True(convertedA.Normal.Dot(convertedB.Normal) < 0d);
        Assert.Equal(openingA.Id, Assert.Single(convertedA.Openings).Id);
        Assert.Equal(openingB.Id, Assert.Single(convertedB.Openings).Id);
        Assert.True(convertedA.Openings[0].Polygon.IsGeometricallyEquivalentTo(
            convertedB.Openings[0].Polygon,
            allowReversedWinding: true));
        DragonConstruction firstConstruction = Assert.IsType<DragonConstruction>(convertedA.Construction);
        DragonConstruction secondConstruction = Assert.IsType<DragonConstruction>(convertedB.Construction);
        Assert.Equal(firstConstruction.Layers.Reverse(), secondConstruction.Layers);
        Assert.Equal(2, result.SurfaceConversions.Count);
        Assert.All(result.SurfaceConversions, item =>
        {
            Assert.Equal(item.SourceZoneId, item.ConvertedZoneId);
            Assert.Equal(item.SourceSurfaceId, item.ConvertedSurfaceId);
            Assert.False(item.IsSynthesizedCounterpart);
        });
        Assert.True(converted.Validate().IsValid, Describe(converted.Validate().Diagnostics));
    }

    [Fact]
    public void OneSidedZoneSurfaceSynthesizesExactlyOneMappedCounterpart()
    {
        GreenRetrofitModel template = GrmReader.ReadFile(Fixture("grm")).RequireModel();
        SurfaceConstruction construction = template.SurfaceConstructions[0];
        Surface sourceSurface = ZoneBoundary("SURFACE-A", "ZONE-B", 12d, construction);
        GreenRetrofitModel source = AdjacencyModel(
            template,
            new[] { sourceSurface },
            Array.Empty<Surface>());

        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(source);

        Assert.True(result.Success, Describe(result));
        EnergyModel converted = result.RequireEnergyModel();
        Assert.Equal(2, converted.Surfaces.Count);
        DragonSurface original = Assert.Single(converted.Surfaces, item => item.Id.Equals(sourceSurface.Id));
        DragonSurface counterpart = Assert.Single(converted.Surfaces, item =>
            item.Id.Value == "CLONE:" + sourceSurface.Id.Value);
        Assert.Equal(counterpart.Id, original.Boundary.AdjacentSurfaceId);
        Assert.Equal(original.Id, counterpart.Boundary.AdjacentSurfaceId);
        Assert.Equal(2, result.SurfaceConversions.Count);
        GreenRetrofitSurfaceConversion synthesized = Assert.Single(
            result.SurfaceConversions,
            item => item.IsSynthesizedCounterpart);
        Assert.Equal(sourceSurface.Id, synthesized.SourceSurfaceId);
        Assert.Equal(new GonieGonie.BuildingEnergy.Contracts.EntityId("ZONE-A"), synthesized.SourceZoneId);
        Assert.Equal(new GonieGonie.BuildingEnergy.Contracts.EntityId("ZONE-B"), synthesized.ConvertedZoneId);
        Assert.Equal(counterpart.Id, synthesized.ConvertedSurfaceId);
    }

    [Fact]
    public void ReciprocalDeclarationsWithDifferentGeometryFailWithMismatchDiagnostic()
    {
        GreenRetrofitModel template = GrmReader.ReadFile(Fixture("grm")).RequireModel();
        SurfaceConstruction construction = template.SurfaceConstructions[0];
        GreenRetrofitModel source = AdjacencyModel(
            template,
            new[] { ZoneBoundary("SURFACE-A", "ZONE-B", 12d, construction) },
            new[] { ZoneBoundary("SURFACE-B", "ZONE-A", 13d, construction) });

        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(source);

        Assert.False(result.Success);
        Assert.Null(result.EnergyModel);
        Assert.Contains(result.Diagnostics, item => item.Code == "SD.CONVERSION.ADJACENCY_MISMATCH");
    }

    [Fact]
    public void IndistinguishableReciprocalCandidatesFailWithAmbiguityDiagnostic()
    {
        GreenRetrofitModel template = GrmReader.ReadFile(Fixture("grm")).RequireModel();
        SurfaceConstruction construction = template.SurfaceConstructions[0];
        GreenRetrofitModel source = AdjacencyModel(
            template,
            new[]
            {
                ZoneBoundary("SURFACE-A1", "ZONE-B", 12d, construction),
                ZoneBoundary("SURFACE-A2", "ZONE-B", 12d, construction),
            },
            new[]
            {
                ZoneBoundary("SURFACE-B1", "ZONE-A", 12d, construction),
                ZoneBoundary("SURFACE-B2", "ZONE-A", 12d, construction),
            });

        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(source);

        Assert.False(result.Success);
        Assert.Null(result.EnergyModel);
        Assert.Contains(result.Diagnostics, item => item.Code == "SD.CONVERSION.ADJACENCY_AMBIGUOUS");
    }

    [Fact]
    public void DistinctReciprocalPairsMatchByGeometryInsteadOfInputOrder()
    {
        GreenRetrofitModel template = GrmReader.ReadFile(Fixture("grm")).RequireModel();
        SurfaceConstruction construction = template.SurfaceConstructions[0];
        GreenRetrofitModel source = AdjacencyModel(
            template,
            new[]
            {
                ZoneBoundary("SURFACE-A12", "ZONE-B", 12d, construction),
                ZoneBoundary("SURFACE-A18", "ZONE-B", 18d, construction),
            },
            new[]
            {
                ZoneBoundary("SURFACE-B18", "ZONE-A", 18d, construction),
                ZoneBoundary("SURFACE-B12", "ZONE-A", 12d, construction),
            });

        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(source);

        Assert.True(result.Success, Describe(result));
        EnergyModel converted = result.RequireEnergyModel();
        Assert.Equal(4, converted.Surfaces.Count);
        Assert.Equal(
            new GonieGonie.BuildingEnergy.Contracts.EntityId("SURFACE-B12"),
            Assert.Single(converted.Surfaces, item => item.Id.Value == "SURFACE-A12")
                .Boundary.AdjacentSurfaceId);
        Assert.Equal(
            new GonieGonie.BuildingEnergy.Contracts.EntityId("SURFACE-B18"),
            Assert.Single(converted.Surfaces, item => item.Id.Value == "SURFACE-A18")
                .Boundary.AdjacentSurfaceId);
    }

    private static Surface ZoneBoundary(
        string id,
        string adjacentZoneId,
        double area,
        SurfaceConstruction construction,
        IEnumerable<Fenestration>? openings = null)
    {
        return new Surface(
            id,
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Zone,
            area,
            null,
            construction.Id.Value,
            construction,
            openings,
            adjacentZoneId: adjacentZoneId,
            id: new GonieGonie.BuildingEnergy.Contracts.EntityId(id));
    }

    private static GreenRetrofitModel AdjacencyModel(
        GreenRetrofitModel template,
        IEnumerable<Surface> firstSurfaces,
        IEnumerable<Surface> secondSurfaces)
    {
        Zone templateZone = Assert.Single(template.Zones);
        var first = new Zone(
            "zone A",
            1,
            templateZone.Height,
            firstSurfaces,
            templateZone.ProfileName,
            templateZone.Profile,
            templateZone.LightDensity,
            id: new GonieGonie.BuildingEnergy.Contracts.EntityId("ZONE-A"));
        var second = new Zone(
            "zone B",
            1,
            templateZone.Height,
            secondSurfaces,
            templateZone.ProfileName,
            templateZone.Profile,
            templateZone.LightDensity,
            id: new GonieGonie.BuildingEnergy.Contracts.EntityId("ZONE-B"));
        return new GreenRetrofitModel(
            "adjacency regression",
            template.NorthAxis,
            template.Address,
            template.Vintage,
            template.IsMultifamilyHousing,
            new[] { new BuildingFloor(1, new[] { first, second }) },
            template.Materials,
            template.SurfaceConstructions,
            template.FenestrationConstructions,
            weather: template.Weather);
    }

    private static string Fixture(string extension)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "fixtures",
                "simple-dragon",
                extension,
                "ASHRAE 140 modified." + extension);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the SimpleDragon fixture.");
    }

    private static string ReferenceIdf()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "fixtures",
                "reference",
                "python-0.7.0",
                "ashrae-140-modified.idf");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the pinned Python IDF oracle.");
    }

    private static string Describe(GreenRetrofitConversionResult result)
    {
        return Describe(result.Diagnostics);
    }

    private static string Describe(IEnumerable<GonieGonie.BuildingEnergy.Contracts.Diagnostic> diagnostics)
    {
        return string.Join(Environment.NewLine, diagnostics.Select(item => item.Code + ": " + item.Message));
    }
}
