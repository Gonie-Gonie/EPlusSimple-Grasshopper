using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

[SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Inline arrays keep the direct Grasshopper contract readable.")]
public sealed class CompositionComponentContractTests
{
    [Fact]
    public void CanonicalCompositionExposesDirectOpeningHvacAndErvOwnership()
    {
        Assembly assembly = LoadPlugin();
        GH_Component opening = Component(assembly, "CreateSimpleDragonOpeningComponent");
        GH_Component zone = Component(assembly, "CreateSimpleDragonZoneComponent");
        GH_Component model = Component(assembly, "CreateSimpleDragonModelComponent");
        GH_Component prepare = Component(assembly, "PrepareSimpleDragonSimulationComponent");

        Assert.Equal(new Guid("7d41fd2c-b93f-4fc8-88ea-db1f3abeb2f1"), opening.ComponentGuid);
        Assert.Equal(new Guid("f7389ac4-51dd-44dc-803a-e8e0989e7638"), zone.ComponentGuid);
        Assert.Equal(new Guid("ce38124b-f99b-4d09-be3b-e5e5717db707"), model.ComponentGuid);
        Assert.Equal(new Guid("ca666fd7-788c-4682-8b04-fad8c7252fe0"), prepare.ComponentGuid);

        Assert.Equal(
            new[]
            {
                "Zone Brep",
                "Name",
                "Floor Number",
                "Profile",
                "Surface Construction",
                "Opening Construction",
                "Openings",
                "HVAC",
                "ERVs",
                "Floor Boundary",
                "Lighting Power Density",
            },
            zone.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal("SimpleDragonOpeningDefinitionParam", zone.Params.Input[6].GetType().Name);
        Assert.Equal("SimpleDragonSupplySystemParam", zone.Params.Input[7].GetType().Name);
        Assert.Equal("SimpleDragonZoneErvParam", zone.Params.Input[8].GetType().Name);
        Assert.All(zone.Params.Input.Skip(6).Take(3), parameter =>
        {
            Assert.Equal(GH_ParamAccess.list, parameter.Access);
            Assert.True(parameter.Optional);
        });

        Assert.Equal("SimpleDragonZoneDefinitionParam", zone.Params.Output[0].GetType().Name);
        Assert.Equal("SimpleDragonZoneDefinitionParam", model.Params.Input[1].GetType().Name);
        Assert.Equal(GH_ParamAccess.list, model.Params.Input[1].Access);
        Assert.Equal("GreenRetrofitModelParam", model.Params.Output[0].GetType().Name);
        Assert.Equal("GreenRetrofitModelParam", prepare.Params.Input[0].GetType().Name);
        Assert.Equal(new[] { "Energy Model", "IDF", "Weather", "Success", "Diagnostics" },
            prepare.Params.Output.Select(parameter => parameter.Name));
    }

    private static GH_Component Component(Assembly assembly, string typeName)
    {
        Type type = Assert.Single(
            assembly.GetTypes(),
            candidate => !candidate.IsAbstract
                && typeof(GH_Component).IsAssignableFrom(candidate)
                && string.Equals(candidate.Name, typeName, StringComparison.Ordinal));
        return Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
    }

    private static Assembly LoadPlugin()
    {
        string path = Path.Combine(
            RepositoryRoot(),
            "temp",
            "build",
            "bin",
            "GonieGonie.SimpleDragon.GH",
            "Release",
            "net8.0-windows",
            "GonieGonie.SimpleDragon.GH.gha");
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
