using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using GH_IO.Serialization;
using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Grasshopper.Parameters;
using Dragons.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace Dragons.SimpleDragon.Grasshopper.Tests;

[SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Inline arrays keep the Grasshopper port contract readable.")]
public sealed class ConstructionLayerCompositionTests
{
    [Fact]
    public void LayerGooDuplicatesAndArchivesAnIndependentLayer()
    {
        var source = new SurfaceConstructionLayer(
            new Material(
                "Simple Insulation",
                0.038d,
                35d,
                1400d,
                new EntityId("MAT-SIMPLE-LAYER")),
            0.16d);
        var goo = new SimpleDragonSurfaceConstructionLayerGoo(source);

        var duplicate = Assert.IsType<SimpleDragonSurfaceConstructionLayerGoo>(goo.Duplicate());
        SimpleDragonSurfaceConstructionLayerGoo archived = ArchiveRoundTrip(
            goo,
            new SimpleDragonSurfaceConstructionLayerGoo());

        AssertLayer(source, duplicate.Value);
        AssertLayer(source, archived.Value);
        Assert.NotSame(source, duplicate.Value);
        Assert.NotSame(source.Material, duplicate.Value.Material);
        Assert.NotSame(source, archived.Value);
        Assert.NotSame(source.Material, archived.Value.Material);
        Assert.Contains("0.16 m", goo.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructionComponentsExposeTypedLayerOwnershipOnly()
    {
        Assembly assembly = LoadPlugin();
        GH_Component layer = Component(
            assembly,
            "SimpleDragonSurfaceConstructionLayerComponent");
        GH_Component construction = Component(
            assembly,
            "SimpleDragonSurfaceConstructionComponent");

        Assert.Equal(new Guid("b97da4a1-7b1c-472a-a4b0-83603e202c2b"), layer.ComponentGuid);
        Assert.Equal(new[] { "Material", "Thickness" },
            layer.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal("SimpleDragonMaterialParam", layer.Params.Input[0].GetType().Name);
        Assert.Equal(
            "SimpleDragonSurfaceConstructionLayerParam",
            layer.Params.Output[0].GetType().Name);

        Assert.Equal(new[] { "Name", "Layers" },
            construction.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(
            "SimpleDragonSurfaceConstructionLayerParam",
            construction.Params.Input[1].GetType().Name);
        Assert.Equal(GH_ParamAccess.list, construction.Params.Input[1].Access);
        Assert.DoesNotContain(
            construction.Params.Input,
            parameter => parameter.Name.Contains("Materials", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("Thicknesses", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LayerParameterUsesFreshStableGuid()
    {
        Assert.Equal(
            new Guid("06f57aae-c0dc-46f9-8af8-e9fa4429fcb7"),
            new SimpleDragonSurfaceConstructionLayerParam().ComponentGuid);
    }

    private static void AssertLayer(
        SurfaceConstructionLayer expected,
        SurfaceConstructionLayer actual)
    {
        Assert.Equal(expected.Thickness, actual.Thickness);
        Assert.Equal(expected.Material.Id, actual.Material.Id);
        Assert.Equal(expected.Material.Name, actual.Material.Name);
        Assert.Equal(expected.Material.Conductivity, actual.Material.Conductivity);
        Assert.Equal(expected.Material.Density, actual.Material.Density);
        Assert.Equal(expected.Material.SpecificHeat, actual.Material.SpecificHeat);
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
