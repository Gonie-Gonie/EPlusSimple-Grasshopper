using System.Collections;
using System.Reflection;
using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Grasshopper.Types;
using GH_IO.Serialization;
using Grasshopper.Kernel;

namespace Dragons.SimpleDragon.Grasshopper.Tests;

public sealed class GeometryMapContextTests
{
    [Fact]
    public void ModelAndResultGoosPreserveHiddenGeometryMapThroughDuplicateAndArchive()
    {
        GreenRetrofitModel model = ReadModel();
        GreenRetrofitResult result = ReadResult();
        GreenRetrofitGeometryMapEntry[] map = GeometryMap();

        var modelGoo = new GreenRetrofitModelGoo(model, map);
        var resultGoo = new GreenRetrofitResultGoo(result, map);
        var modelDuplicate = Assert.IsType<GreenRetrofitModelGoo>(modelGoo.Duplicate());
        var resultDuplicate = Assert.IsType<GreenRetrofitResultGoo>(resultGoo.Duplicate());
        GreenRetrofitModelGoo modelReopened = ArchiveRoundTrip(modelGoo, new GreenRetrofitModelGoo());
        GreenRetrofitResultGoo resultReopened = ArchiveRoundTrip(resultGoo, new GreenRetrofitResultGoo());

        AssertMap(map, modelDuplicate.GeometryMap);
        AssertMap(map, resultDuplicate.GeometryMap);
        AssertMap(map, modelReopened.GeometryMap);
        AssertMap(map, resultReopened.GeometryMap);
        Assert.NotSame(model, modelDuplicate.Value);
        Assert.NotSame(result, resultDuplicate.Value);
        Assert.DoesNotContain(map[0].Provenance.GeometryFingerprint, GrmWriter.Serialize(modelGoo.Value), StringComparison.Ordinal);
        Assert.DoesNotContain(map[0].Provenance.GeometryFingerprint, GrrWriter.Serialize(resultGoo.Value), StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<GreenRetrofitGeometryMapEntry>>(modelGoo.GeometryMap)
                .Add(map[0]));
    }

    [Fact]
    public void GooCastCopiesOnlyGrasshopperOwnedGeometryContext()
    {
        GreenRetrofitModel model = ReadModel();
        GreenRetrofitResult result = ReadResult();
        GreenRetrofitGeometryMapEntry[] map = GeometryMap();
        var modelSource = new GreenRetrofitModelGoo(model, map);
        var resultSource = new GreenRetrofitResultGoo(result, map);
        var modelTarget = new GreenRetrofitModelGoo(model, map);
        var resultTarget = new GreenRetrofitResultGoo(result, map);

        Assert.True(modelTarget.CastFrom(modelSource));
        Assert.True(resultTarget.CastFrom(resultSource));
        AssertMap(map, modelTarget.GeometryMap);
        AssertMap(map, resultTarget.GeometryMap);

        Assert.True(modelTarget.CastFrom(model));
        Assert.True(resultTarget.CastFrom(result));
        Assert.Empty(modelTarget.GeometryMap);
        Assert.Empty(resultTarget.GeometryMap);
    }

    [Fact]
    public void ExportCsvReadsGeometryContextWithoutAPublicMapPort()
    {
        GreenRetrofitModel model = ReadModel();
        GreenRetrofitResult result = ReadResult();
        GreenRetrofitGeometryMapEntry[] map = GeometryMap();

        AssertExportContainsMap(
            new GreenRetrofitResultGoo(result, map),
            modelGoo: null,
            map[0]);
        AssertExportContainsMap(
            new GreenRetrofitResultGoo(result),
            new GreenRetrofitModelGoo(model, map),
            map[0]);
    }

    private static void AssertExportContainsMap(
        GreenRetrofitResultGoo resultGoo,
        GreenRetrofitModelGoo? modelGoo,
        GreenRetrofitGeometryMapEntry expected)
    {
        GH_Component component = Component("ExportGreenRetrofitCsvComponent");
        IGH_DataAccess access = DispatchProxy.Create<IGH_DataAccess, ExportDataAccess>();
        var probe = Assert.IsAssignableFrom<ExportDataAccess>(access);
        probe.Inputs[0] = resultGoo;
        if (modelGoo is not null)
        {
            probe.Inputs[1] = modelGoo;
        }

        probe.Inputs[2] = Path.Combine(Path.GetTempPath(), "simpledragon-geometry-map-preview");
        probe.Inputs[4] = false;
        probe.Inputs[5] = false;

        InvokeSolve(component, access);

        string[] names = Values<string>(probe.Lists[1]);
        string[] content = Values<string>(probe.Lists[3]);
        int geometryIndex = Array.IndexOf(names, GreenRetrofitCsvExporter.GeometryMapFileName);
        Assert.True(geometryIndex >= 0);
        Assert.Contains(expected.EntityId.Value, content[geometryIndex], StringComparison.Ordinal);
        Assert.Contains(expected.Provenance.GeometryFingerprint, content[geometryIndex], StringComparison.Ordinal);
        Assert.Empty(component.RuntimeMessages(GH_RuntimeMessageLevel.Error));
    }

    private static GreenRetrofitGeometryMapEntry[] GeometryMap()
    {
        return
        [
            new GreenRetrofitGeometryMapEntry(
                new EntityId("ZONE-CONTEXT-TEST"),
                GreenRetrofitGeometryKind.Zone,
                0,
                null,
                null,
                null,
                new GeometryProvenance(
                    new Guid("1ff39348-81a0-4dc6-a844-7c1f538499ae"),
                    null,
                    "sha256:geometry-context-test",
                    "{4;2}",
                    3)),
        ];
    }

    private static void AssertMap(
        IReadOnlyList<GreenRetrofitGeometryMapEntry> expected,
        IReadOnlyList<GreenRetrofitGeometryMapEntry> actual)
    {
        GreenRetrofitGeometryMapEntry expectedEntry = Assert.Single(expected);
        GreenRetrofitGeometryMapEntry actualEntry = Assert.Single(actual);
        Assert.Equal(expectedEntry.EntityId, actualEntry.EntityId);
        Assert.Equal(expectedEntry.Kind, actualEntry.Kind);
        Assert.Equal(expectedEntry.ZoneIndex, actualEntry.ZoneIndex);
        Assert.Equal(expectedEntry.SurfaceIndex, actualEntry.SurfaceIndex);
        Assert.Equal(expectedEntry.OpeningIndex, actualEntry.OpeningIndex);
        Assert.Equal(expectedEntry.TrimLoopIndex, actualEntry.TrimLoopIndex);
        Assert.Equal(expectedEntry.Provenance, actualEntry.Provenance);
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

    private static GreenRetrofitModel ReadModel() =>
        GrmReader.ReadFile(Fixture("grm", "ASHRAE 140 modified.grm")).RequireModel();

    private static GreenRetrofitResult ReadResult() =>
        GrrReader.ReadFile(Fixture("grr", "ASHRAE 140 modified.grr")).RequireResult();

    private static string Fixture(string kind, string fileName) => Path.Combine(
        FindRepositoryRoot(AppContext.BaseDirectory),
        "fixtures",
        "simple-dragon",
        kind,
        fileName);

    private static GH_Component Component(string typeName)
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
            candidate => candidate.Name == typeName);
        return Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
    }

    private static void InvokeSolve(GH_Component component, IGH_DataAccess access)
    {
        MethodInfo solve = Assert.IsAssignableFrom<MethodInfo>(component.GetType().BaseType?.GetMethod(
            "SolveInstance",
            BindingFlags.Instance | BindingFlags.NonPublic));
        solve.Invoke(component, new object[] { access });
    }

    private static T[] Values<T>(IEnumerable values) =>
        values.Cast<object>().Select(Assert.IsType<T>).ToArray();

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
    private class ExportDataAccess : DispatchProxy
    {
        internal Dictionary<int, object?> Inputs { get; } = new();

        internal Dictionary<int, IEnumerable> Lists { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.NotNull(targetMethod);
            if (string.Equals(targetMethod!.Name, "GetData", StringComparison.Ordinal)
                && args is { Length: 2 }
                && args[0] is int inputIndex
                && Inputs.TryGetValue(inputIndex, out object? value))
            {
                args[1] = value;
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
