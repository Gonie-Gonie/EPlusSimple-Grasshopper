using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;

namespace Dragons.SimpleDragon.Grasshopper.Tests;

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
        GH_Component window = Component(assembly, "CreateSimpleDragonWindowComponent");
        GH_Component door = Component(assembly, "CreateSimpleDragonDoorComponent");
        GH_Component glassDoor = Component(assembly, "CreateSimpleDragonGlassDoorComponent");
        GH_Component floor = Component(assembly, "CreateSimpleDragonFloorComponent");
        GH_Component ceiling = Component(assembly, "CreateSimpleDragonCeilingComponent");
        GH_Component wall = Component(assembly, "CreateSimpleDragonWallComponent");
        GH_Component zone = Component(assembly, "CreateSimpleDragonZoneComponent");
        GH_Component model = Component(assembly, "CreateSimpleDragonModelComponent");
        GH_Component run = Component(assembly, "RunSimpleDragonComponent");

        AssertOpeningContract(
            window,
            new Guid("ce46938c-f720-4ca5-839b-50b0ca33a58f"),
            "SimpleDragon Window",
            "SD Window",
            "Window",
            FenestrationType.Window,
            supportsBlind: true);
        AssertOpeningContract(
            door,
            new Guid("f293420c-85bd-4bb7-a62b-1c2b9de3ab26"),
            "SimpleDragon Door",
            "SD Door",
            "Door",
            FenestrationType.Door,
            supportsBlind: false);
        AssertOpeningContract(
            glassDoor,
            new Guid("c60ad628-b4b7-4db7-ae47-bc2c806b0291"),
            "SimpleDragon Glass Door",
            "SD GlassDoor",
            "Glass Door",
            FenestrationType.GlassDoor,
            supportsBlind: true);
        Assert.Equal(new Guid("e15d7475-e5cf-4e37-81a4-e656c69ee250"), floor.ComponentGuid);
        Assert.Equal(new Guid("39e2ad8c-8fbb-40bd-84cc-218de37bb720"), ceiling.ComponentGuid);
        Assert.Equal(new Guid("2c0bc0e2-df1d-4e42-9b97-d841e8c83214"), wall.ComponentGuid);
        Assert.Equal(new Guid("30b8e2c4-207a-4cf5-9801-ac4ae16d33e2"), zone.ComponentGuid);
        Assert.Equal(new Guid("ce38124b-f99b-4d09-be3b-e5e5717db707"), model.ComponentGuid);
        Assert.Equal(new Guid("6e242e51-77ce-4f77-8445-a17d636c7310"), run.ComponentGuid);

        foreach (GH_Component surface in new[] { floor, wall })
        {
            Assert.Equal(
                new[] { "Face", "Name", "Construction", "Boundary Condition", "Openings" },
                surface.Params.Input.Select(parameter => parameter.Name));
        }

        Assert.Equal(
            new[]
            {
                "Face",
                "Name",
                "Construction",
                "Boundary Condition",
                "Openings",
                "Cool Roof Reflectance",
            },
            ceiling.Params.Input.Select(parameter => parameter.Name));
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
            },
            zone.Params.Input.Select(parameter => parameter.Name));
        Assert.DoesNotContain(
            zone.Params.Input,
            parameter => parameter.GetType().Name == "SimpleDragonFenestrationConstructionParam");
        Assert.DoesNotContain(zone.Params.Input, parameter => parameter.Name == "Zone Brep");
        Assert.DoesNotContain(zone.Params.Input, parameter => parameter.Name == "Surface Construction");
        Assert.DoesNotContain(zone.Params.Input, parameter => parameter.Name == "Openings");
        Assert.DoesNotContain(zone.Params.Input, parameter => parameter.Name == "Floor Boundary");
        Assert.Equal("Ground", StringDefault(floor.Params.Input[3]));
        Assert.Equal("Outdoors", StringDefault(ceiling.Params.Input[3]));
        Assert.Equal("Outdoors", StringDefault(wall.Params.Input[3]));
        foreach (GH_Component surface in new[] { floor, ceiling, wall })
        {
            Assert.Equal(GH_ParamAccess.item, surface.Params.Input[0].Access);
            Assert.Equal("SimpleDragonSurfaceConstructionParam", surface.Params.Input[2].GetType().Name);
            Assert.True(surface.Params.Input[2].Optional);
            Assert.Equal("ChoiceStringParam", surface.Params.Input[3].GetType().Name);
            Assert.Equal(GH_ParamAccess.item, surface.Params.Input[3].Access);
            Assert.Equal("SimpleDragonOpeningDefinitionParam", surface.Params.Input[4].GetType().Name);
            Assert.Equal(GH_ParamAccess.list, surface.Params.Input[4].Access);
            Assert.True(surface.Params.Input[4].Optional);
            Assert.Equal("SimpleDragonSurfaceDefinitionParam", surface.Params.Output[0].GetType().Name);
            Assert.Equal(GH_ParamAccess.item, surface.Params.Output[0].Access);
        }
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
        Assert.Equal(GH_ParamAccess.tree, model.Params.Output[2].Access);
        Assert.Equal(
            new[] { "GRM", "Zones", "Surfaces", "JSON", "Diagnostics" },
            model.Params.Output.Select(parameter => parameter.Name));
        Assert.DoesNotContain(
            model.Params.Output,
            parameter => parameter.Name.Contains("Map", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parameter.Name, "Floor Area", StringComparison.Ordinal));
        Assert.Equal("GreenRetrofitModelParam", run.Params.Input[0].GetType().Name);
        Assert.Equal(
            new[] { "GRM", "Run", "Cancel", "Force Rerun", "Timeout", "GRR Path" },
            run.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(
            new[] { "GRR", "State", "Success", "Diagnostics" },
            run.Params.Output.Select(parameter => parameter.Name));
        Assert.Equal("GreenRetrofitResultParam", run.Params.Output[0].GetType().Name);
        Assert.Equal("SimpleDragonDiagnosticParam", run.Params.Output[3].GetType().Name);
    }

    [Fact]
    public void GenericOpeningComponentAndGuidDoNotRemainInTheAssembly()
    {
        Assembly assembly = LoadPlugin();
        GH_Component[] components = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(GH_Component).IsAssignableFrom(type))
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .ToArray();

        Assert.Null(assembly.GetType(
            "Dragons.SimpleDragon.Grasshopper.Components.CreateSimpleDragonOpeningComponent",
            throwOnError: false,
            ignoreCase: false));
        Assert.DoesNotContain(
            new Guid("7d41fd2c-b93f-4fc8-88ea-db1f3abeb2f1"),
            components.Select(component => component.ComponentGuid));
        Assert.DoesNotContain(components, component =>
            string.Equals(component.Name, "SimpleDragon Opening", StringComparison.Ordinal)
            || string.Equals(component.NickName, "SD Opening", StringComparison.Ordinal));
    }

    private static void AssertOpeningContract(
        GH_Component component,
        Guid expectedGuid,
        string expectedName,
        string expectedNickname,
        string expectedDefaultName,
        FenestrationType expectedType,
        bool supportsBlind)
    {
        Assert.Equal(expectedGuid, component.ComponentGuid);
        Assert.Equal(expectedName, component.Name);
        Assert.Equal(expectedNickname, component.NickName);
        Assert.Equal("SimpleDragon", component.Category);
        Assert.Equal("Geometry", component.SubCategory);
        Assert.Equal(
            supportsBlind
                ? new[] { "Boundary", "Name", "Construction", "Blind" }
                : new[] { "Boundary", "Name", "Construction" },
            component.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(
            supportsBlind
                ? new[] { "C", "N", "FC", "Blind" }
                : new[] { "C", "N", "FC" },
            component.Params.Input.Select(parameter => parameter.NickName));
        Assert.All(
            component.Params.Input,
            parameter => Assert.Equal(GH_ParamAccess.item, parameter.Access));
        Assert.DoesNotContain(component.Params.Input, parameter => parameter.Name == "Type");
        Assert.IsType<Param_Curve>(component.Params.Input[0]);
        Assert.IsType<Param_String>(component.Params.Input[1]);
        Assert.Equal(expectedDefaultName, StringDefault(component.Params.Input[1]));
        Assert.Equal(
            "SimpleDragonFenestrationConstructionParam",
            component.Params.Input[2].GetType().Name);
        Assert.False(component.Params.Input[2].Optional);
        if (supportsBlind)
        {
            Assert.Equal("ChoiceStringParam", component.Params.Input[3].GetType().Name);
            Assert.Equal("None", StringDefault(component.Params.Input[3]));
        }
        else
        {
            Assert.DoesNotContain(component.Params.Input, parameter => parameter.Name == "Blind");
        }

        Assert.Equal(
            new[] { "Opening", "Diagnostics" },
            component.Params.Output.Select(parameter => parameter.Name));
        Assert.Equal(
            new[] { "O", "D" },
            component.Params.Output.Select(parameter => parameter.NickName));
        Assert.Equal("SimpleDragonOpeningDefinitionParam", component.Params.Output[0].GetType().Name);
        Assert.Equal(GH_ParamAccess.item, component.Params.Output[0].Access);
        Assert.Equal("SimpleDragonDiagnosticParam", component.Params.Output[1].GetType().Name);
        Assert.Equal(GH_ParamAccess.list, component.Params.Output[1].Access);

        PropertyInfo? fixedType = component.GetType().GetProperty(
            "FixedOpeningType",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(fixedType);
        Assert.Equal(expectedType, Assert.IsType<FenestrationType>(fixedType.GetValue(component)));
    }

    private static string StringDefault(IGH_Param parameter)
    {
        Param_String choice = Assert.IsAssignableFrom<Param_String>(parameter);
        return Assert.IsType<GH_String>(Assert.Single(choice.PersistentData.AllData(true))).Value;
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
                "Dragons.InvisibleDragon",
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
