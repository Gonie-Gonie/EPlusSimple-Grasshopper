using System.Collections;
using System.Reflection;
using Dragons.SimpleDragon.Grasshopper.Parameters;
using Dragons.SimpleDragon.Grasshopper.Types;
using GH_IO.Serialization;
using Grasshopper.Kernel;

namespace Dragons.SimpleDragon.Grasshopper.Tests;

public sealed class GreenRetrofitModelSummaryComponentTests
{
    private static readonly string[] OutputNames =
    {
        "Floor Area",
        "Exterior Floors",
        "Exterior Roofs",
        "Exterior Walls",
        "Exterior Windows",
        "Average Exterior Floor U-Value",
        "Average Exterior Roof U-Value",
        "Average Exterior Wall U-Value",
        "Average Window U-Value",
        "Average Infiltration at 50 Pa",
        "Average Lighting Power Density",
        "Climate Region",
        "Terrain",
        "Weather Location",
    };

    [Fact]
    public void PortsExposeTheTypedModelSummaryContract()
    {
        GH_Component component = Component();

        Assert.Equal("SimpleDragon Model Summary", component.Name);
        Assert.Equal("SD Model Summary", component.NickName);
        Assert.Equal("SimpleDragon", component.Category);
        Assert.Equal("Model", component.SubCategory);
        Assert.Equal(new Guid("f2e7bb6b-9cf9-4833-9069-b9be4089e1b3"), component.ComponentGuid);
        Assert.Single(component.Params.Input);
        Assert.IsType<GreenRetrofitModelParam>(component.Params.Input[0]);
        Assert.Equal("GRM", component.Params.Input[0].Name);
        Assert.Equal(GH_ParamAccess.item, component.Params.Input[0].Access);
        Assert.Equal(OutputNames, component.Params.Output.Select(parameter => parameter.Name));
        Assert.Equal(
            new[]
            {
                GH_ParamAccess.item,
                GH_ParamAccess.list,
                GH_ParamAccess.list,
                GH_ParamAccess.list,
                GH_ParamAccess.list,
                GH_ParamAccess.item,
                GH_ParamAccess.item,
                GH_ParamAccess.item,
                GH_ParamAccess.item,
                GH_ParamAccess.item,
                GH_ParamAccess.item,
                GH_ParamAccess.item,
                GH_ParamAccess.item,
                GH_ParamAccess.item,
            },
            component.Params.Output.Select(parameter => parameter.Access));
        Assert.All(component.Params.Output.Skip(1).Take(3), output =>
            Assert.IsType<SimpleDragonSurfaceParam>(output));
        Assert.IsType<SimpleDragonFenestrationParam>(component.Params.Output[4]);
        Assert.DoesNotContain(
            component.Params.Output,
            output => output.Name.Contains("EPW", StringComparison.OrdinalIgnoreCase)
                || output.Description.Contains("EPW", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SolveUsesTheCoreModelDerivedPropertiesWithoutRecalculation()
    {
        GreenRetrofitModel model = ReadFixtureModel();
        ModelSummaryDataAccess probe = Solve(model);

        Assert.Equal(model.Area, Assert.IsType<double>(probe.Items[0]), 12);
        Assert.Equal(
            model.ExteriorFloors.Select(surface => surface.Id),
            Values<SimpleDragonSurfaceGoo>(probe.Lists[1]).Select(goo => goo.Value.Id));
        Assert.Equal(
            model.ExteriorRoofs.Select(surface => surface.Id),
            Values<SimpleDragonSurfaceGoo>(probe.Lists[2]).Select(goo => goo.Value.Id));
        Assert.Equal(
            model.ExteriorWalls.Select(surface => surface.Id),
            Values<SimpleDragonSurfaceGoo>(probe.Lists[3]).Select(goo => goo.Value.Id));
        Assert.Equal(
            model.ExteriorWindows.Select(window => window.Id),
            Values<SimpleDragonFenestrationGoo>(probe.Lists[4]).Select(goo => goo.Value.Id));
        Assert.Equal(model.AverageExteriorFloorUValue, Assert.IsType<double>(probe.Items[5]), 12);
        Assert.Equal(model.AverageExteriorRoofUValue, Assert.IsType<double>(probe.Items[6]), 12);
        Assert.Equal(model.AverageExteriorWallUValue, Assert.IsType<double>(probe.Items[7]), 12);
        Assert.Equal(model.AverageWindowUValue, Assert.IsType<double>(probe.Items[8]), 12);
        Assert.Equal(model.AverageInfiltration, Assert.IsType<double>(probe.Items[9]), 12);
        Assert.Equal(model.AverageLightDensity, Assert.IsType<double>(probe.Items[10]), 12);
        Assert.Equal(model.Weather!.ClimateRegion, probe.Items[11]);
        Assert.Equal(model.Weather.Terrain, probe.Items[12]);
        Assert.Equal(model.Weather.WeatherLocation, probe.Items[13]);
        Assert.Empty(probe.Component.RuntimeMessages(GH_RuntimeMessageLevel.Warning));
    }

    [Fact]
    public void MissingWeatherLeavesOnlyWeatherOutputsUnsetAndReportsAWarning()
    {
        GreenRetrofitModel source = ReadFixtureModel();
        var model = new GreenRetrofitModel(
            source.Name,
            source.NorthAxis,
            source.Address,
            source.Vintage,
            source.IsMultifamilyHousing,
            source.Floors,
            source.Materials,
            source.SurfaceConstructions,
            source.FenestrationConstructions,
            source.SourceSystems,
            source.SupplySystems,
            source.VentilationSystems,
            source.PhotovoltaicSystems,
            weather: null);

        ModelSummaryDataAccess probe = Solve(model);

        Assert.All(Enumerable.Range(0, 11), index =>
            Assert.True(probe.Items.ContainsKey(index) || probe.Lists.ContainsKey(index)));
        Assert.DoesNotContain(11, probe.Items.Keys);
        Assert.DoesNotContain(12, probe.Items.Keys);
        Assert.DoesNotContain(13, probe.Items.Keys);
        string warning = Assert.Single(probe.Component.RuntimeMessages(GH_RuntimeMessageLevel.Warning));
        Assert.Contains("no resolved weather metadata", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(probe.Component.RuntimeMessages(GH_RuntimeMessageLevel.Error));
    }

    [Fact]
    public void FenestrationGooDuplicatesAndRoundTripsItsTypedSnapshot()
    {
        Fenestration source = ReadFixtureModel().ExteriorWindows[0];
        var goo = new SimpleDragonFenestrationGoo(source);

        var duplicate = Assert.IsType<SimpleDragonFenestrationGoo>(goo.Duplicate());
        SimpleDragonFenestrationGoo reopened = ArchiveRoundTrip(
            goo,
            new SimpleDragonFenestrationGoo());

        Assert.NotSame(source, duplicate.Value);
        Assert.Equal(source.Id, duplicate.Value.Id);
        Assert.Equal(source.Type, duplicate.Value.Type);
        Assert.Equal(source.Construction!.Id, duplicate.Value.Construction!.Id);
        Assert.NotSame(source, reopened.Value);
        Assert.Equal(source.Id, reopened.Value.Id);
        Assert.Equal(source.Area, reopened.Value.Area);
        Assert.Equal(source.Blind, reopened.Value.Blind);
        Assert.Equal(source.Construction.Id, reopened.Value.Construction!.Id);
    }

    private static ModelSummaryDataAccess Solve(GreenRetrofitModel model)
    {
        GH_Component component = Component();
        IGH_DataAccess access = DispatchProxy.Create<IGH_DataAccess, ModelSummaryDataAccess>();
        var probe = Assert.IsAssignableFrom<ModelSummaryDataAccess>(access);
        probe.Component = component;
        probe.Model = new GreenRetrofitModelGoo(model);
        InvokeSolve(component, access);
        return probe;
    }

    private static T[] Values<T>(IEnumerable values)
    {
        return values.Cast<object>().Select(Assert.IsType<T>).ToArray();
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

    private static GreenRetrofitModel ReadFixtureModel()
    {
        GrmReadResult read = GrmReader.ReadFile(Path.Combine(
            FindRepositoryRoot(AppContext.BaseDirectory),
            "fixtures",
            "simple-dragon",
            "grm",
            "ASHRAE 140 modified.grm"));
        Assert.True(
            read.Success,
            string.Join(Environment.NewLine, read.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        return read.RequireModel();
    }

    private static GH_Component Component()
    {
        Assembly assembly = Assembly.LoadFrom(Path.Combine(
            FindRepositoryRoot(AppContext.BaseDirectory),
            "temp",
            "build",
            "bin",
            "Dragons.SimpleDragon.GH",
            "Release",
            "net8.0-windows",
            "Dragons.SimpleDragon.GH.gha"));
        Type type = Assert.Single(
            assembly.GetTypes(),
            candidate => candidate.Name == "GreenRetrofitModelSummaryComponent");
        return Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
    }

    private static void InvokeSolve(GH_Component component, IGH_DataAccess access)
    {
        MethodInfo solve = Assert.IsAssignableFrom<MethodInfo>(component.GetType().BaseType?.GetMethod(
            "SolveInstance",
            BindingFlags.Instance | BindingFlags.NonPublic));
        solve.Invoke(component, new object[] { access });
    }

    private static string FindRepositoryRoot(string start)
    {
        DirectoryInfo? current = new(start);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dragons.Grasshopper.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy creates a runtime subclass of this probe.")]
    private class ModelSummaryDataAccess : DispatchProxy
    {
        internal GH_Component Component { get; set; } = null!;

        internal GreenRetrofitModelGoo Model { get; set; } = null!;

        internal Dictionary<int, object?> Items { get; } = new();

        internal Dictionary<int, IEnumerable> Lists { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.NotNull(targetMethod);
            if (string.Equals(targetMethod!.Name, "GetData", StringComparison.Ordinal)
                && args is { Length: 2 }
                && args[0] is 0)
            {
                args[1] = Model;
                return true;
            }

            if (string.Equals(targetMethod.Name, "SetData", StringComparison.Ordinal)
                && args is { Length: 2 }
                && args[0] is int itemIndex)
            {
                Items[itemIndex] = args[1];
                return true;
            }

            if (string.Equals(targetMethod.Name, "SetDataList", StringComparison.Ordinal)
                && args is { Length: 2 }
                && args[0] is int listIndex
                && args[1] is IEnumerable values)
            {
                Lists[listIndex] = values.Cast<object>().ToArray();
                return true;
            }

            if (targetMethod.ReturnType == typeof(bool))
            {
                return false;
            }

            if (targetMethod.ReturnType == typeof(int))
            {
                return 0;
            }

            return null;
        }
    }
}
