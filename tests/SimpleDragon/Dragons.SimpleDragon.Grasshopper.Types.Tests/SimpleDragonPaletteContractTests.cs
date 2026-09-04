using System.Reflection;
using Grasshopper.Kernel;

namespace Dragons.SimpleDragon.Grasshopper.Tests;

public sealed class SimpleDragonPaletteContractTests
{
    private static readonly string[] ExpectedPanelNames =
    {
        "Analysis",
        "Construction",
        "Geometry",
        "Model",
        "Simulation",
    };

    private static readonly IReadOnlyDictionary<string, string> ExpectedSubcategories =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SimpleDragonMaterialComponent"] = "Construction",
            ["SimpleDragonSurfaceConstructionLayerComponent"] = "Construction",
            ["SimpleDragonSurfaceConstructionComponent"] = "Construction",
            ["SimpleDragonFenestrationConstructionComponent"] = "Construction",

            ["CreateSimpleDragonOpeningComponent"] = "Geometry",
            ["CreateSimpleDragonFloorComponent"] = "Geometry",
            ["CreateSimpleDragonCeilingComponent"] = "Geometry",
            ["CreateSimpleDragonWallComponent"] = "Geometry",
            ["CreateSimpleDragonZoneComponent"] = "Geometry",

            ["LookupUsageProfileComponent"] = "Model",
            ["CreateSimpleDragonModelComponent"] = "Model",
            ["SimpleDragonHeatPumpComponent"] = "Model",
            ["SimpleDragonGeothermalHeatPumpComponent"] = "Model",
            ["SimpleDragonChillerComponent"] = "Model",
            ["SimpleDragonAbsorptionChillerComponent"] = "Model",
            ["SimpleDragonBoilerComponent"] = "Model",
            ["SimpleDragonDistrictHeatingComponent"] = "Model",
            ["SimpleDragonPackagedAirConditionerComponent"] = "Model",
            ["SimpleDragonAirHandlingUnitComponent"] = "Model",
            ["SimpleDragonFanCoilUnitComponent"] = "Model",
            ["SimpleDragonRadiatorComponent"] = "Model",
            ["SimpleDragonElectricRadiatorComponent"] = "Model",
            ["SimpleDragonRadiantFloorComponent"] = "Model",
            ["SimpleDragonElectricRadiantFloorComponent"] = "Model",
            ["SimpleDragonEnergyRecoveryVentilatorComponent"] = "Model",
            ["SimpleDragonPhotovoltaicPanelComponent"] = "Model",

            ["RunSimpleDragonComponent"] = "Simulation",
            ["ReadGreenRetrofitModelComponent"] = "Simulation",
            ["WriteGreenRetrofitModelComponent"] = "Simulation",
            ["ReadGreenRetrofitResultComponent"] = "Simulation",
            ["WriteGreenRetrofitResultComponent"] = "Simulation",
            ["ExportGreenRetrofitCsvComponent"] = "Simulation",
            ["SimpleDragonBatchCaseComponent"] = "Simulation",
            ["ManagedRunSimpleDragonBatchComponent"] = "Simulation",

            ["GreenRetrofitModelSummaryComponent"] = "Analysis",
            ["GreenRetrofitResultSummaryComponent"] = "Analysis",
            ["GreenRetrofitDataTreeComponent"] = "Analysis",
            ["GreenRetrofitMonthlyLinePlotComponent"] = "Analysis",
            ["GreenRetrofitMonthlyBarPlotComponent"] = "Analysis",
        };

    [Fact]
    public void EveryPublicComponentUsesTheExactFivePanelPalette()
    {
        Assembly assembly = LoadPlugin();
        GH_Component[] components = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(GH_Component).IsAssignableFrom(type))
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .OrderBy(component => component.GetType().Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedSubcategories.Count, components.Length);
        Assert.Equal(
            ExpectedSubcategories.Keys.OrderBy(name => name, StringComparer.Ordinal),
            components.Select(component => component.GetType().Name));
        Assert.All(components, component =>
        {
            Assert.Equal("SimpleDragon", component.Category);
            Assert.Equal(
                ExpectedSubcategories[component.GetType().Name],
                component.SubCategory);
        });
        Assert.Equal(
            ExpectedPanelNames,
            components.Select(component => component.SubCategory)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal));
        Assert.DoesNotContain(
            components,
            component => string.Equals(component.SubCategory, "Core", StringComparison.Ordinal)
                || string.Equals(component.SubCategory, "Results", StringComparison.Ordinal));
    }

    [Fact]
    public void DeletedSimpleDragonInfoDoesNotRemainInTheAssembly()
    {
        Assembly assembly = LoadPlugin();
        GH_Component[] components = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(GH_Component).IsAssignableFrom(type))
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .ToArray();

        Assert.Null(assembly.GetType(
            "Dragons.SimpleDragon.Grasshopper.Components.SimpleDragonVersionComponent",
            throwOnError: false,
            ignoreCase: false));
        Assert.DoesNotContain(
            new Guid("ea29f1c8-72aa-446a-8da4-c786ab470237"),
            components.Select(component => component.ComponentGuid));
        Assert.DoesNotContain(components, component =>
            string.Equals(component.Name, "SimpleDragon Version", StringComparison.Ordinal)
            || string.Equals(component.NickName, "SimpleDragonVersion", StringComparison.Ordinal)
            || string.Equals(component.NickName, "SD Info", StringComparison.Ordinal));
    }

    private static Assembly LoadPlugin()
    {
        string path = Path.Combine(
            RepositoryRoot(),
            "temp",
            "build",
            "bin",
            "Dragons.SimpleDragon.GH",
            "Release",
            "net8.0-windows",
            "Dragons.SimpleDragon.GH.gha");
        Assert.True(File.Exists(path), "Expected built Grasshopper assembly at '" + path + "'.");
        return Assembly.LoadFrom(path);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null
            && !File.Exists(Path.Combine(current.FullName, "Dragons.Grasshopper.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
