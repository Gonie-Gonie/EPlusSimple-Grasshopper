using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

[SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Inline arrays keep the direct Grasshopper contract readable.")]
public sealed class CompositionComponentContractTests
{
    [Fact]
    public void CanonicalCompositionExposesOpeningSurfaceZoneOwnership()
    {
        Assembly assembly = LoadPlugin();
        GH_Component opening = Component(assembly, "CreateSimpleDragonOpeningComponent");
        GH_Component surface = Component(assembly, "CreateSimpleDragonSurfaceComponent");
        GH_Component zone = Component(assembly, "CreateSimpleDragonZoneComponent");
        GH_Component model = Component(assembly, "CreateSimpleDragonModelComponent");
        GH_Component run = Component(assembly, "RunSimpleDragonComponent");

        Assert.Equal(new Guid("7d41fd2c-b93f-4fc8-88ea-db1f3abeb2f1"), opening.ComponentGuid);
        Assert.Equal(new Guid("039bf7bb-da65-49e2-80fe-86d636cf0a48"), surface.ComponentGuid);
        Assert.Equal(new Guid("30b8e2c4-207a-4cf5-9801-ac4ae16d33e2"), zone.ComponentGuid);
        Assert.Equal(new Guid("ce38124b-f99b-4d09-be3b-e5e5717db707"), model.ComponentGuid);
        Assert.Equal(new Guid("6e242e51-77ce-4f77-8445-a17d636c7310"), run.ComponentGuid);

        Assert.Equal(
            new[]
            {
                "Face",
                "Name",
                "Type",
                "Construction",
                "Boundary Intent",
                "Openings",
                "Cool Roof Reflectance",
                "ID",
            },
            surface.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(
            new[]
            {
                "Surfaces",
                "Name",
                "Floor Number",
                "Height",
                "Profile",
                "HVAC",
                "ERVs",
                "Lighting Power Density",
                "ID",
            },
            zone.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal("SimpleDragonFenestrationConstructionParam", opening.Params.Input[3].GetType().Name);
        Assert.False(opening.Params.Input[3].Optional);
        Assert.DoesNotContain(
            zone.Params.Input,
            parameter => parameter.GetType().Name == "SimpleDragonFenestrationConstructionParam");
        Assert.DoesNotContain(zone.Params.Input, parameter => parameter.Name == "Zone Brep");
        Assert.DoesNotContain(zone.Params.Input, parameter => parameter.Name == "Surface Construction");
        Assert.DoesNotContain(zone.Params.Input, parameter => parameter.Name == "Openings");
        Assert.DoesNotContain(zone.Params.Input, parameter => parameter.Name == "Floor Boundary");
        Param_Integer surfaceType = Assert.IsType<Param_Integer>(surface.Params.Input[2]);
        Param_Integer boundaryIntent = Assert.IsType<Param_Integer>(surface.Params.Input[4]);
        Assert.True(surfaceType.HasNamedValues);
        Assert.True(boundaryIntent.HasNamedValues);
        Assert.Equal("SimpleDragonSurfaceConstructionParam", surface.Params.Input[3].GetType().Name);
        Assert.True(surface.Params.Input[3].Optional);
        Assert.Equal("SimpleDragonOpeningDefinitionParam", surface.Params.Input[5].GetType().Name);
        Assert.Equal(GH_ParamAccess.list, surface.Params.Input[5].Access);
        Assert.True(surface.Params.Input[5].Optional);
        Assert.Equal("SimpleDragonSurfaceDefinitionParam", surface.Params.Output[0].GetType().Name);
        Assert.Equal("SimpleDragonSurfaceDefinitionParam", zone.Params.Input[0].GetType().Name);
        Assert.Equal(GH_ParamAccess.list, zone.Params.Input[0].Access);
        Assert.False(zone.Params.Input[0].Optional);
        Assert.Equal("SimpleDragonSupplySystemParam", zone.Params.Input[5].GetType().Name);
        Assert.Equal("SimpleDragonZoneErvParam", zone.Params.Input[6].GetType().Name);
        Assert.All(zone.Params.Input.Skip(5).Take(2), parameter =>
        {
            Assert.Equal(GH_ParamAccess.list, parameter.Access);
            Assert.True(parameter.Optional);
        });

        Assert.Equal("SimpleDragonZoneDefinitionParam", zone.Params.Output[0].GetType().Name);
        Assert.Equal("SimpleDragonZoneDefinitionParam", model.Params.Input[1].GetType().Name);
        Assert.Equal(GH_ParamAccess.list, model.Params.Input[1].Access);
        Assert.Equal("GreenRetrofitModelParam", model.Params.Output[0].GetType().Name);
        Assert.Equal("GreenRetrofitModelParam", run.Params.Input[0].GetType().Name);
        Assert.Equal(
            new[] { "GRM", "Run", "Cancel", "Force Rerun", "Timeout" },
            run.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(
            new[] { "GRR", "State", "Success", "Diagnostics" },
            run.Params.Output.Select(parameter => parameter.Name));
        Assert.Equal("GreenRetrofitResultParam", run.Params.Output[0].GetType().Name);
        Assert.Equal("SimpleDragonDiagnosticParam", run.Params.Output[3].GetType().Name);
    }

    [Fact]
    public void PublicSimpleDragonPortsDoNotExposeInvisibleDragonTypesOrLegacyStages()
    {
        Assembly assembly = LoadPlugin();
        Type[] componentTypes = assembly.GetTypes()
            .Where(candidate => !candidate.IsAbstract
                && typeof(GH_Component).IsAssignableFrom(candidate))
            .ToArray();
        Assert.DoesNotContain(componentTypes, type => type.Name == "PrepareSimpleDragonSimulationComponent");
        Assert.DoesNotContain(componentTypes, type => type.Name == "BuildGreenRetrofitResultComponent");

        GH_Component[] components = componentTypes
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .ToArray();
        Assert.All(
            components.SelectMany(component => component.Params.Input.Concat(component.Params.Output)),
            parameter => Assert.DoesNotContain(
                "GonieGonie.InvisibleDragon",
                parameter.GetType().FullName ?? string.Empty,
                StringComparison.Ordinal));
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
