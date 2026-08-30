using System.Text.Json;
using GH_IO.Serialization;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

public sealed class SimpleDragonGooTests
{
    [Fact]
    public void EveryCoreGooDuplicatesAnIndependentDomainGraph()
    {
        TestDomain domain = CreateDomain();

        Assert.NotSame(domain.Material, Assert.IsType<SimpleDragonMaterialGoo>(
            new SimpleDragonMaterialGoo(domain.Material).Duplicate()).Value);
        Assert.NotSame(domain.SurfaceConstruction, Assert.IsType<SimpleDragonSurfaceConstructionGoo>(
            new SimpleDragonSurfaceConstructionGoo(domain.SurfaceConstruction).Duplicate()).Value);
        Assert.NotSame(domain.FenestrationConstruction, Assert.IsType<SimpleDragonFenestrationConstructionGoo>(
            new SimpleDragonFenestrationConstructionGoo(domain.FenestrationConstruction).Duplicate()).Value);
        Assert.NotSame(domain.Profile, Assert.IsType<SimpleDragonUsageProfileGoo>(
            new SimpleDragonUsageProfileGoo(domain.Profile).Duplicate()).Value);
        Assert.NotSame(domain.Zone.Surfaces[0], Assert.IsType<SimpleDragonSurfaceGoo>(
            new SimpleDragonSurfaceGoo(domain.Zone.Surfaces[0]).Duplicate()).Value);

        var zoneCopy = Assert.IsType<SimpleDragonZoneGoo>(new SimpleDragonZoneGoo(domain.Zone).Duplicate()).Value;
        Assert.NotSame(domain.Zone, zoneCopy);
        Assert.Equal(domain.Zone.Id, zoneCopy.Id);
        Assert.Equal(domain.Zone.Profile!.OperatingDays, zoneCopy.Profile!.OperatingDays);
        Assert.Equal(domain.Zone.SupplySystems[0].Id, zoneCopy.SupplySystems[0].Id);
        Assert.Equal(domain.Zone.VentilationAssignments[0].Count, zoneCopy.VentilationAssignments[0].Count);

        var modelCopy = Assert.IsType<GreenRetrofitModelGoo>(
            new GreenRetrofitModelGoo(domain.Model).Duplicate()).Value;
        Assert.NotSame(domain.Model, modelCopy);
        Assert.Equal(domain.Model.Id, modelCopy.Id);
        Assert.Equal(domain.Model.Zones[0].Id, modelCopy.Zones[0].Id);
        Assert.Equal(domain.Model.SourceSystems[0].Id, modelCopy.SourceSystems[0].Id);
        Assert.Equal(domain.Model.PhotovoltaicSystems[0].Id, modelCopy.PhotovoltaicSystems[0].Id);
        Assert.Equal(domain.Model.Weather!.EpwFileName, modelCopy.Weather!.EpwFileName);

        GreenRetrofitResult result = CreateResult(domain.Model.Area);
        var resultCopy = Assert.IsType<GreenRetrofitResultGoo>(
            new GreenRetrofitResultGoo(result).Duplicate()).Value;
        Assert.NotSame(result, resultCopy);
        Assert.Equal(
            result.PerAreaSummaries[GreenRetrofitMetric.SiteUses].MonthlyTotal,
            resultCopy.PerAreaSummaries[GreenRetrofitMetric.SiteUses].MonthlyTotal);
    }

    [Fact]
    public void GrasshopperArchiveRoundTripsModelAndResultSnapshots()
    {
        TestDomain domain = CreateDomain();
        GreenRetrofitResult result = CreateResult(domain.Model.Area);

        GreenRetrofitModelGoo modelCopy = ArchiveRoundTrip(
            new GreenRetrofitModelGoo(domain.Model),
            new GreenRetrofitModelGoo());
        GreenRetrofitResultGoo resultCopy = ArchiveRoundTrip(
            new GreenRetrofitResultGoo(result),
            new GreenRetrofitResultGoo());

        Assert.Equal(domain.Model.Zones[0].Surfaces[1].Fenestrations[0].Id,
            modelCopy.Value.Zones[0].Surfaces[1].Fenestrations[0].Id);
        Assert.Equal(
            result.GrossSummaries[GreenRetrofitMetric.Cost].AnnualTotal,
            resultCopy.Value.GrossSummaries[GreenRetrofitMetric.Cost].AnnualTotal);
    }

    [Fact]
    public void SimpleDragonDiagnosticGooDuplicatesAndRoundTripsItsOwnSnapshot()
    {
        var diagnostic = new Diagnostic(
            "SD.TEST.DIAGNOSTIC",
            DiagnosticSeverity.Warning,
            "SimpleDragon diagnostic snapshot",
            suggestedAction: "Review the connected GRM.");

        SimpleDragonDiagnosticGoo duplicate = Assert.IsType<SimpleDragonDiagnosticGoo>(
            new SimpleDragonDiagnosticGoo(diagnostic).Duplicate());
        SimpleDragonDiagnosticGoo reopened = ArchiveRoundTrip(
            new SimpleDragonDiagnosticGoo(diagnostic),
            new SimpleDragonDiagnosticGoo());

        Assert.NotSame(diagnostic, duplicate.Value);
        Assert.Equal(diagnostic, duplicate.Value);
        Assert.NotSame(diagnostic, reopened.Value);
        Assert.Equal(diagnostic, reopened.Value);
    }

    [Theory]
    [InlineData(UsageProfileSource.Standard, 0)]
    [InlineData(UsageProfileSource.Extended, 1)]
    [InlineData(UsageProfileSource.Custom, 2)]
    public void UsageProfileSourceOrdinalsRoundTripGrasshopperArchives(
        UsageProfileSource source,
        int expectedOrdinal)
    {
        UsageProfile profile = CreateProfile(source);

        SimpleDragonUsageProfileGoo reopened = ArchiveRoundTrip(
            new SimpleDragonUsageProfileGoo(profile),
            new SimpleDragonUsageProfileGoo());

        Assert.Equal(expectedOrdinal, (int)source);
        Assert.Equal(source, reopened.Value.Source);
        Assert.Equal(profile.Id, reopened.Value.Id);
        Assert.Equal(profile.OperatingDays, reopened.Value.OperatingDays);
        Assert.Equal(
            profile.Vacations.Select(period => (period.Start.ToString(), period.End.ToString())),
            reopened.Value.Vacations.Select(period => (period.Start.ToString(), period.End.ToString())));
    }

    [Fact]
    public void GrasshopperArchivePreservesCanonicalGrmOptionalFieldPresence()
    {
        const string supplyMarker = "\"supply_systems\": {";
        string fixture = File.ReadAllText(GetFixturePath());
        string withDefaultedPackagedSystem = ReplaceFirst(
            fixture,
            supplyMarker,
            supplyMarker
                + " \"packaged_air_conditioner\": [{"
                + " \"id\": \"SUPL-PACKAGED\","
                + " \"name\": \"Defaulted packaged system\" }],");
        GrmReadResult read = GrmReader.Read(withDefaultedPackagedSystem);
        Assert.True(read.Success, Describe(read));
        GreenRetrofitModel model = read.RequireModel();

        SupplySystem packaged = Assert.Single(
            model.SupplySystems,
            system => system.Type == SupplySystemType.PackagedAirConditioner);
        Assert.Equal(3d, packaged.CoolingCop);

        string expected = GrmWriter.Serialize(model);
        using (JsonDocument document = JsonDocument.Parse(expected))
        {
            JsonElement building = document.RootElement.GetProperty("building");
            JsonElement packagedJson = building.GetProperty("supply_systems")
                .GetProperty("packaged_air_conditioner")[0];
            JsonElement boilerJson = building.GetProperty("source_systems").GetProperty("boiler")[0];
            Assert.False(packagedJson.TryGetProperty("cop_cooling", out _));
            Assert.False(boilerJson.TryGetProperty("efficiency", out _));
        }

        GreenRetrofitModelGoo reopened = ArchiveRoundTrip(
            new GreenRetrofitModelGoo(model),
            new GreenRetrofitModelGoo());

        Assert.Equal(expected, GrmWriter.Serialize(reopened.Value));
    }

    [Fact]
    public void GooCastsExposeDomainAndScriptValues()
    {
        Material material = CreateDomain().Material;
        var goo = new SimpleDragonMaterialGoo();

        Assert.True(goo.CastFrom(material));
        Material? cast = null;
        Assert.True(goo.CastTo(ref cast));
        Assert.Same(material, cast);
        Assert.Same(material, goo.ScriptVariable());
        Assert.Contains(material.Name, goo.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PublicParameterGuidsAreUniqueAndStable()
    {
        Guid[] identifiers =
        {
            new SimpleDragonDiagnosticParam().ComponentGuid,
            new SimpleDragonMaterialParam().ComponentGuid,
            new SimpleDragonSurfaceConstructionLayerParam().ComponentGuid,
            new SimpleDragonSurfaceConstructionParam().ComponentGuid,
            new SimpleDragonFenestrationConstructionParam().ComponentGuid,
            new SimpleDragonUsageProfileParam().ComponentGuid,
            new SimpleDragonSurfaceParam().ComponentGuid,
            new SimpleDragonZoneParam().ComponentGuid,
            new SimpleDragonOpeningDefinitionParam().ComponentGuid,
            new SimpleDragonSurfaceDefinitionParam().ComponentGuid,
            new SimpleDragonZoneDefinitionParam().ComponentGuid,
            new SimpleDragonSourceSystemParam().ComponentGuid,
            new SimpleDragonSupplySystemParam().ComponentGuid,
            new SimpleDragonZoneErvParam().ComponentGuid,
            new SimpleDragonPhotovoltaicPanelParam().ComponentGuid,
            new GreenRetrofitModelParam().ComponentGuid,
            new SimpleDragonBatchCaseParam().ComponentGuid,
            new GreenRetrofitResultParam().ComponentGuid,
        };

        Assert.Equal(18, identifiers.Length);
        Assert.Equal(identifiers.Length, identifiers.Distinct().Count());
        Assert.Contains(new Guid("e54751c3-4d56-4499-83fb-f833822cf6bb"), identifiers);
        Assert.Contains(new Guid("e0546c97-2fba-4c51-9613-340dfb1fc416"), identifiers);
        Assert.Contains(new Guid("11dead46-9ee4-48ce-913e-50ff7f10d319"), identifiers);
        Assert.Contains(new Guid("51b809c1-a4ae-4dc7-bca8-81e06d49a806"), identifiers);
        Assert.Contains(new Guid("51610fe9-ecf1-43b4-9157-7260b3ba89ad"), identifiers);
        Assert.Contains(new Guid("14feee1f-498c-478c-92ac-4bd0e9d256da"), identifiers);
        Assert.Contains(new Guid("df2c89ba-56a7-48ea-83f2-ba58ac15f17f"), identifiers);
        Assert.Contains(new Guid("14f1683e-4b0a-4754-aac5-6b85331c2126"), identifiers);
        Assert.Contains(new Guid("731f38e6-55dd-4d1e-b9cb-ae33faf23154"), identifiers);
        Assert.Contains(new Guid("c30c8d9a-15bd-4dd1-b1dd-3d1d3a2d7169"), identifiers);
    }

    private static TGoo ArchiveRoundTrip<TGoo>(TGoo source, TGoo target)
        where TGoo : GH_IO.GH_ISerializable
    {
        var writeArchive = new GH_Archive();
        Assert.True(writeArchive.AppendObject(source, "Value"));
        byte[] bytes = writeArchive.Serialize_Binary();
        var readArchive = new GH_Archive();
        Assert.True(readArchive.Deserialize_Binary(bytes));
        Assert.True(readArchive.ExtractObject(target, "Value"));
        return target;
    }

    private static string GetFixturePath()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "fixtures",
                "simple-dragon",
                "grm",
                "ASHRAE 140 modified.grm");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the GRM fixture from the test output directory.");
    }

    private static string ReplaceFirst(string source, string oldValue, string newValue)
    {
        int index = source.IndexOf(oldValue, StringComparison.Ordinal);
        Assert.True(index >= 0, "The fixture token to replace was not found.");
        return source.Remove(index, oldValue.Length).Insert(index, newValue);
    }

    private static string Describe(GrmReadResult result)
    {
        return string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message));
    }

    private static GreenRetrofitResult CreateResult(double area)
    {
        EnergyUseBreakdown siteUses = EnergyUseBreakdown.Create((endUse, carrier) =>
            Enumerable.Range(1, MonthlySeries.MonthCount).Select(month =>
                endUse == EnergyEndUse.Heating && carrier == EnergyCarrier.Electricity
                    ? month / 10d
                    : 0d));
        return GreenRetrofitResult.FromSiteUses(area, siteUses);
    }

    private static UsageProfile CreateProfile(UsageProfileSource source)
    {
        var operation = ((UsageDay[])Enum.GetValues(typeof(UsageDay)))
            .ToDictionary(day => day, _ => true);
        return new UsageProfile(
            "Custom Profile",
            8,
            18,
            7,
            19,
            4,
            0.2,
            10,
            0.1,
            8,
            20,
            26,
            operation,
            new[] { new VacationPeriod(new MonthDay(8, 1), new MonthDay(8, 7)) },
            source,
            new EntityId("PROFILE-TEST"));
    }

    private static TestDomain CreateDomain()
    {
        var material = new Material("Insulation", 0.04, 30, 1400, new EntityId("MATERIAL-TEST"));
        var surfaceConstruction = new SurfaceConstruction(
            "Envelope",
            new[] { new SurfaceConstructionLayer(material, 0.2) },
            new EntityId("CONSTRUCTION-TEST"));
        var fenestrationConstruction = new FenestrationConstruction(
            "Window",
            1.4,
            0.45,
            new EntityId("FENESTRATION-CONSTRUCTION-TEST"));
        UsageProfile profile = CreateProfile(UsageProfileSource.Extended);
        var window = new Fenestration(
            "Window",
            FenestrationType.Window,
            2,
            fenestrationConstruction.Id.Value,
            fenestrationConstruction,
            BlindType.Shade,
            new EntityId("WINDOW-TEST"));
        var floor = new Surface(
            "Floor",
            SurfaceType.Floor,
            SurfaceBoundaryCondition.Ground,
            20,
            null,
            surfaceConstruction.Id.Value,
            surfaceConstruction,
            id: new EntityId("FLOOR-TEST"));
        var wall = new Surface(
            "North Wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            12,
            0,
            surfaceConstruction.Id.Value,
            surfaceConstruction,
            new[] { window },
            id: new EntityId("WALL-TEST"));
        var source = new SourceSystem(
            "Boiler",
            SourceSystemType.Boiler,
            FuelType.NaturalGas,
            efficiency: 0.9,
            hotWaterSupply: true,
            id: new EntityId("SOURCE-TEST"));
        var supply = new SupplySystem(
            "Radiant Floor",
            SupplySystemType.RadiantFloor,
            source.Id.Value,
            source,
            id: new EntityId("SUPPLY-TEST"));
        var ventilation = new VentilationSystem(
            "ERV",
            0.2,
            id: new EntityId("VENTILATION-TEST"));
        var zone = new Zone(
            "Test Zone",
            1,
            2.7,
            new[] { floor, wall },
            profile.Name,
            profile,
            9,
            new[] { new SupplySystemAssignment(supply.Id.Value, supply) },
            new[] { new VentilationAssignment(ventilation.Id.Value, 2, ventilation) },
            new EntityId("ZONE-TEST"));
        WeatherSelection weather = SimpleDragonDatabase.Default.Weather.FindByAddress(
            "서울특별시 종로구",
            new DateTime(2020, 1, 1)).Require();
        var photovoltaic = new PhotovoltaicSystem(
            "Roof PV",
            10,
            0.2,
            180,
            30,
            new EntityId("PV-TEST"));
        var model = new GreenRetrofitModel(
            "Test Model",
            15,
            "서울특별시 종로구",
            new DateTime(2020, 1, 1),
            false,
            new[] { new BuildingFloor(1, new[] { zone }) },
            new[] { material },
            new[] { surfaceConstruction },
            new[] { fenestrationConstruction },
            new[] { source },
            new[] { supply },
            new[] { ventilation },
            new[] { photovoltaic },
            weather);
        return new TestDomain(material, surfaceConstruction, fenestrationConstruction, profile, zone, model);
    }

    private sealed record TestDomain(
        Material Material,
        SurfaceConstruction SurfaceConstruction,
        FenestrationConstruction FenestrationConstruction,
        UsageProfile Profile,
        Zone Zone,
        GreenRetrofitModel Model);
}
