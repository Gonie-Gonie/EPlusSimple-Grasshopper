using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using GH_IO.Serialization;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

[SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Inline arrays keep each Grasshopper port-contract assertion local and readable.")]
public sealed class BatchCaseAuthoringTests
{
    private const string ComponentTypeName =
        "GonieGonie.SimpleDragon.Grasshopper.Components.SimpleDragonBatchCaseComponent";

    [Fact]
    public void BatchCaseOwnsItsModelAndValidatesItsOptionalStableId()
    {
        GreenRetrofitModel model = AddressOnlyModel();

        var batchCase = new SimpleDragonBatchCase(model, "  option-a  ");
        var derivedIdCase = new SimpleDragonBatchCase(model);

        Assert.NotSame(model, batchCase.Model);
        Assert.Equal(GrmWriter.Serialize(model), GrmWriter.Serialize(batchCase.Model));
        Assert.Equal("option-a", batchCase.CaseId);
        Assert.Null(derivedIdCase.CaseId);
        Assert.Throws<ArgumentException>(() => new SimpleDragonBatchCase(model, "not a valid ID"));
    }

    [Fact]
    public void BatchCaseGooDuplicatesAndRoundTripsItsCompleteModelGraph()
    {
        var original = new SimpleDragonBatchCase(AddressOnlyModel(), "option-a");
        var goo = new SimpleDragonBatchCaseGoo(original);

        var duplicate = Assert.IsType<SimpleDragonBatchCaseGoo>(goo.Duplicate());
        SimpleDragonBatchCaseGoo archived = ArchiveRoundTrip(
            goo,
            new SimpleDragonBatchCaseGoo());

        AssertEquivalent(original, duplicate.Value);
        AssertEquivalent(original, archived.Value);
        Assert.Contains("option-a", goo.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void BatchCaseComponentExposesOnlyDirectCompositionInputs()
    {
        GH_Component component = Component();

        Assert.Equal(new Guid("11336c6a-5bd4-4d6b-80a1-89bd168f8d54"), component.ComponentGuid);
        Assert.Equal(GH_Exposure.primary, component.Exposure);
        Assert.Equal(new[] { "GRM", "Case ID" }, component.Params.Input.Select(item => item.Name));
        Assert.IsType<GreenRetrofitModelParam>(component.Params.Input[0]);
        Assert.True(component.Params.Input[1].Optional);
        Assert.Equal(new[] { "Case" }, component.Params.Output.Select(item => item.Name));
        Assert.IsType<SimpleDragonBatchCaseParam>(component.Params.Output[0]);
        Assert.All(
            component.Params.Input,
            parameter => Assert.DoesNotContain(
                new[] { "Path", "EPW", "IDD", "Runtime", "Weather" },
                forbidden => parameter.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void BatchCaseParameterIsPublicAndHasAFreshGuid()
    {
        Type[] exported = typeof(SimpleDragonBatchCaseGoo).Assembly.GetExportedTypes();
        var parameter = new SimpleDragonBatchCaseParam();

        Assert.Contains(typeof(SimpleDragonBatchCase), exported);
        Assert.Contains(typeof(SimpleDragonBatchCaseGoo), exported);
        Assert.Contains(typeof(SimpleDragonBatchCaseParam), exported);
        Assert.Equal(new Guid("c30c8d9a-15bd-4dd1-b1dd-3d1d3a2d7169"), parameter.ComponentGuid);
    }

    private static void AssertEquivalent(
        SimpleDragonBatchCase expected,
        SimpleDragonBatchCase actual)
    {
        Assert.NotSame(expected, actual);
        Assert.NotSame(expected.Model, actual.Model);
        Assert.Equal(expected.CaseId, actual.CaseId);
        Assert.Equal(GrmWriter.Serialize(expected.Model), GrmWriter.Serialize(actual.Model));
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

    private static GreenRetrofitModel AddressOnlyModel()
    {
        WeatherMetadata metadata = SimpleDragonDatabase.Default.Weather.Items[0];
        return new GreenRetrofitModel(
            "Batch Case Address Model",
            0,
            metadata.AdministrativeArea,
            new DateTime(2020, 1, 1),
            false,
            Array.Empty<BuildingFloor>(),
            Array.Empty<Material>(),
            Array.Empty<SurfaceConstruction>(),
            Array.Empty<FenestrationConstruction>());
    }

    private static GH_Component Component()
    {
        Assembly assembly = Assembly.LoadFrom(Path.Combine(
            RepositoryRoot(),
            "temp",
            "build",
            "bin",
            "GonieGonie.SimpleDragon.GH",
            "Release",
            "net8.0-windows",
            "GonieGonie.SimpleDragon.GH.gha"));
        Type type = Assert.IsAssignableFrom<Type>(assembly.GetType(ComponentTypeName));
        return Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
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
