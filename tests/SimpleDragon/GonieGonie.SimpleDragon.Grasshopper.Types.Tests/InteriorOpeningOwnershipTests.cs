using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

[SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Inline arrays make the small reconciliation matrices explicit in each test.")]
public sealed class InteriorOpeningOwnershipTests
{
    private const string ResolverTypeName =
        "GonieGonie.SimpleDragon.Grasshopper.Components.InteriorOpeningOwnershipResolver";

    [Fact]
    public void OneSidedOpeningPlansExactlyOneCounterpart()
    {
        Assert.Equal("add-second:0", Assert.Single(BuildPlan(
            new[] { "opening-a" },
            new[] { "window|construction-a" },
            Array.Empty<string>(),
            Array.Empty<string>())));
    }

    [Fact]
    public void IdenticalExplicitOpeningOnBothSidesPlansNoDuplicate()
    {
        Assert.Empty(BuildPlan(
            new[] { "opening-a" },
            new[] { "window|construction-a" },
            new[] { "opening-a" },
            new[] { "window|construction-a" }));
    }

    [Fact]
    public void ConflictingExplicitMetadataPlansClearConflict()
    {
        Assert.Equal("conflict:0:0", Assert.Single(BuildPlan(
            new[] { "opening-a" },
            new[] { "window|construction-a" },
            new[] { "opening-a" },
            new[] { "door|construction-b" })));
    }

    [Fact]
    public void NonIdenticalExplicitTopologyOnBothSidesFailsInsteadOfMerging()
    {
        Assert.Equal("conflict-topology", Assert.Single(BuildPlan(
            new[] { "opening-a", "opening-b" },
            new[] { "metadata-a", "metadata-b" },
            new[] { "opening-b", "opening-c" },
            new[] { "metadata-b", "metadata-c" })));
    }

    [Fact]
    public void MirroredExplicitIdIsDistinctAndGeometryStable()
    {
        Type type = ResolverType();
        MethodInfo method = Assert.IsAssignableFrom<MethodInfo>(type.GetMethod(
            "PairedIdForTesting",
            BindingFlags.Static | BindingFlags.NonPublic));

        string first = Assert.IsType<string>(method.Invoke(
            null,
            new object[] { "OPENING-A", "0123456789ABCDEF-remainder" }));
        string repeated = Assert.IsType<string>(method.Invoke(
            null,
            new object[] { "OPENING-A", "0123456789ABCDEF-remainder" }));
        string otherFace = Assert.IsType<string>(method.Invoke(
            null,
            new object[] { "OPENING-A", "FEDCBA9876543210-remainder" }));

        Assert.NotEqual("OPENING-A", first);
        Assert.Equal(first, repeated);
        Assert.NotEqual(first, otherFace);
        Assert.Equal("OPENING-A-PAIR-0123456789ABCDEF", first);
    }

    private static IReadOnlyList<string> BuildPlan(
        IReadOnlyList<string> firstGeometry,
        IReadOnlyList<string> firstMetadata,
        IReadOnlyList<string> secondGeometry,
        IReadOnlyList<string> secondMetadata)
    {
        Type type = ResolverType();
        MethodInfo method = Assert.IsAssignableFrom<MethodInfo>(type.GetMethod(
            "BuildPlanForTesting",
            BindingFlags.Static | BindingFlags.NonPublic));
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(method.Invoke(
            null,
            new object[] { firstGeometry, firstMetadata, secondGeometry, secondMetadata }));
    }

    private static Type ResolverType() =>
        Assert.IsAssignableFrom<Type>(LoadPlugin().GetType(ResolverTypeName));

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
