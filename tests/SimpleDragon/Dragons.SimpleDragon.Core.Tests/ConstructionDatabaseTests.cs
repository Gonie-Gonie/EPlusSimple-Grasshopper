namespace Dragons.SimpleDragon.Tests;

public sealed class ConstructionDatabaseTests
{
    [Fact]
    public void MaterialDatabaseMatchesRepresentativePythonValuesAndSourceOrder()
    {
        MaterialDatabase database = SimpleDragonDatabase.Default.Materials;

        Assert.Collection(
            database.Items,
            material => Assert.Equal("concrete", material.Name),
            material => Assert.Equal("insulation", material.Name),
            material => Assert.Equal("gypsumboard", material.Name),
            material => Assert.Equal("glasswool", material.Name));
        Material glassWool = database.Find("glasswool").Require();
        Assert.Equal(0.035d, glassWool.Conductivity);
        Assert.Equal(19d, glassWool.Density);
        Assert.Equal(960d, glassWool.SpecificHeat);
    }

    [Fact]
    public void SurfaceRegulationQueryUsesLatestDateAndUpstreamPartRules()
    {
        SurfaceConstructionDatabase database = SimpleDragonDatabase.Default.SurfaceConstructions;

        Assert.Equal(1344, database.Entries.Count);
        Assert.Equal(14, database.RegulationDates.Count);
        SurfaceConstruction result = database.FindRegulated(
            new DateTime(2020, 1, 1),
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            "중부2",
            isMultifamilyHousing: true).Require();

        Assert.Equal("20180901&외벽&외기 직접&공동주택&중부2", result.Name);
        Assert.Equal(0.17d, result.GetUValue(), 10);
        Assert.Collection(
            result.Layers,
            layer => Assert.Equal("insulation", layer.Material.Name),
            layer => Assert.Equal("concrete", layer.Material.Name));
    }

    [Theory]
    [InlineData(SurfaceType.Wall, SurfaceBoundaryCondition.Outdoors, false, "외벽", 0.24d)]
    [InlineData(SurfaceType.Ceiling, SurfaceBoundaryCondition.Outdoors, false, "최상층 지붕", 0.15d)]
    [InlineData(SurfaceType.Ceiling, SurfaceBoundaryCondition.AdjacentSpace, false, "바닥난방이 아닌 층간바닥", 1.16d)]
    [InlineData(SurfaceType.Ceiling, SurfaceBoundaryCondition.AdjacentSpace, true, "바닥난방인 층간바닥", 0.81d)]
    [InlineData(SurfaceType.Floor, SurfaceBoundaryCondition.Ground, false, "바닥난방이 아닌 최하층 바닥", 0.2d)]
    [InlineData(SurfaceType.Floor, SurfaceBoundaryCondition.Ground, true, "바닥난방인 최하층 바닥", 0.17d)]
    [InlineData(SurfaceType.Floor, SurfaceBoundaryCondition.AdjacentSpace, false, "바닥난방이 아닌 층간바닥", 1.16d)]
    [InlineData(SurfaceType.Floor, SurfaceBoundaryCondition.AdjacentSpace, true, "바닥난방인 층간바닥", 0.81d)]
    public void EveryUpstreamSurfacePartBranchSelectsExpectedRegulation(
        SurfaceType surfaceType,
        SurfaceBoundaryCondition boundaryCondition,
        bool isRadiantFloor,
        string expectedPart,
        double expectedUValue)
    {
        SurfaceConstruction result = SimpleDragonDatabase.Default.SurfaceConstructions.FindRegulated(
            new DateTime(2020, 1, 1),
            surfaceType,
            boundaryCondition,
            "중부2",
            isRadiantFloor).Require();

        Assert.Equal("20180901&" + expectedPart + "&외기 직접&공동주택 외&중부2", result.Name);
        Assert.Equal(expectedUValue, result.GetUValue(), 10);
    }

    [Fact]
    public void FenestrationDatabaseMatchesFirstAndLastPythonRows()
    {
        FenestrationConstructionDatabase database = SimpleDragonDatabase.Default.FenestrationConstructions;

        Assert.Equal(432, database.Entries.Count);
        FenestrationConstruction first = database.Find(new FenestrationConstructionKey(
            "단창", "없음", "미주입", "미적용", "금속재", "6mm")).Require();
        FenestrationConstruction last = database.Find(new FenestrationConstructionKey(
            "사중창", "소프트코팅", "주입", "적용", "목재", "16mm")).Require();

        Assert.Equal(6.6d, first.UValue);
        Assert.Equal(0.717d, first.SolarHeatGainCoefficient);
        Assert.Equal(1.2d, last.UValue);
        Assert.Equal(0.466d, last.SolarHeatGainCoefficient);
    }

    [Fact]
    public void UnknownConstructionInputsReturnStableDiagnostics()
    {
        SimpleDragonDatabase database = SimpleDragonDatabase.Default;

        LookupResult<Material> missingMaterial = database.Materials.Find("unobtainium");
        LookupResult<SurfaceConstruction> missingClimate = database.SurfaceConstructions.FindRegulated(
            new DateTime(2020, 1, 1),
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            "not-a-region");

        Assert.False(missingMaterial.Found);
        Assert.Equal("SD.DB.MATERIAL_NOT_FOUND", Assert.Single(missingMaterial.Diagnostics).Code);
        Assert.False(missingClimate.Found);
        Assert.Equal("SD.DB.SURFACE_CONSTRUCTION_NOT_FOUND", Assert.Single(missingClimate.Diagnostics).Code);
    }

    [Fact]
    public void DatabaseReloadProducesIdenticalOrderAndIdentifiers()
    {
        SimpleDragonDatabase first = SimpleDragonDatabase.LoadEmbedded();
        SimpleDragonDatabase second = SimpleDragonDatabase.LoadEmbedded();

        Assert.Equal(
            first.Materials.Items.Select(item => item.Id.Value),
            second.Materials.Items.Select(item => item.Id.Value));
        Assert.Equal(
            first.SurfaceConstructions.Entries.Select(item => item.Construction.Id.Value),
            second.SurfaceConstructions.Entries.Select(item => item.Construction.Id.Value));
        Assert.Equal(
            first.FenestrationConstructions.Entries.Select(item => item.Construction.Id.Value),
            second.FenestrationConstructions.Entries.Select(item => item.Construction.Id.Value));
        Assert.Equal(
            first.UsageProfiles.Items.Select(item => item.Id.Value),
            second.UsageProfiles.Items.Select(item => item.Id.Value));
        Assert.Equal(
            first.Holidays.Items.Select(item => item.Id.Value),
            second.Holidays.Items.Select(item => item.Id.Value));
        Assert.Equal(
            first.Weather.Items.Select(item => item.Id.Value),
            second.Weather.Items.Select(item => item.Id.Value));
    }
}
