using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using GH_IO.Serialization;
using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Grasshopper.Parameters;
using Dragons.InvisibleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace Dragons.InvisibleDragon.Grasshopper.Tests;

[SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Inline arrays keep the Grasshopper port contract readable.")]
public sealed class ConstructionLayerCompositionTests
{
    [Fact]
    public void LayerGooDuplicatesAndArchivesAnIndependentLayer()
    {
        var source = new Layer(
            "Exterior Insulation",
            new Material("Mineral Wool", 0.039d, 42d, 1030d),
            0.14d);
        var goo = new DragonLayerGoo(source);

        var duplicate = Assert.IsType<DragonLayerGoo>(goo.Duplicate());
        DragonLayerGoo archived = ArchiveRoundTrip(goo, new DragonLayerGoo());

        AssertLayer(source, duplicate.Value);
        AssertLayer(source, archived.Value);
        Assert.NotSame(source, duplicate.Value);
        Assert.NotSame(source.Material, duplicate.Value.Material);
        Assert.NotSame(source, archived.Value);
        Assert.NotSame(source.Material, archived.Value.Material);
        Assert.Contains("0.14 m", goo.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructionComponentsExposeTypedLayerOwnershipOnly()
    {
        Assembly assembly = LoadPlugin();
        GH_Component layer = Component(assembly, "ConstructionLayerComponent");
        GH_Component construction = Component(assembly, "LayeredConstructionComponent");

        Assert.Equal(new Guid("d15984d5-cd3f-4798-a67c-73138b54859e"), layer.ComponentGuid);
        Assert.Equal(new[] { "Material", "Thickness", "Name" },
            layer.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal("DragonMaterialParam", layer.Params.Input[0].GetType().Name);
        Assert.Equal("DragonLayerParam", layer.Params.Output[0].GetType().Name);

        Assert.Equal(new[] { "Name", "Layers" },
            construction.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal("DragonLayerParam", construction.Params.Input[1].GetType().Name);
        Assert.Equal(GH_ParamAccess.list, construction.Params.Input[1].Access);
        Assert.DoesNotContain(
            construction.Params.Input,
            parameter => parameter.Name.Contains("Material", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("Thickness", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LayerParameterUsesFreshStableGuid()
    {
        Assert.Equal(
            new Guid("bf8556b8-ce25-4f0e-8f50-3ca49463e9d4"),
            new DragonLayerParam().ComponentGuid);
    }

    private static void AssertLayer(Layer expected, Layer actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.ThicknessMetres, actual.ThicknessMetres);
        Assert.Equal(expected.Material.Name, actual.Material.Name);
        Assert.Equal(
            expected.Material.ConductivityWattsPerMetreKelvin,
            actual.Material.ConductivityWattsPerMetreKelvin);
        Assert.Equal(
            expected.Material.DensityKilogramsPerCubicMetre,
            actual.Material.DensityKilogramsPerCubicMetre);
        Assert.Equal(
            expected.Material.SpecificHeatJoulesPerKilogramKelvin,
            actual.Material.SpecificHeatJoulesPerKilogramKelvin);
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
            "Dragons.InvisibleDragon.GH",
            "Release",
            "net8.0-windows",
            "Dragons.InvisibleDragon.GH.gha");
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
