using System.Reflection;
using GonieGonie.BuildingEnergy.Contracts;
using Rhino;
using Rhino.Geometry;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

public sealed class OpeningHostResolverTests
{
    private const string ResolverTypeName =
        "GonieGonie.SimpleDragon.Grasshopper.Components.OpeningHostResolver";

    [Fact]
    public void CoplanarContainedOpeningResolvesExactlyOneHostFace()
    {
        RunWithNativeGeometry(() =>
        {
            using Brep zone = ZoneBrep();
            using PolylineCurve opening = OpeningCurve(y: 0d);

            Resolution resolution = Resolve(zone, opening, absoluteTolerance: 0.001d);

            Assert.True(resolution.IsSuccess);
            Assert.NotNull(resolution.FaceIndex);
            Assert.InRange(resolution.FaceIndex.Value, 0, zone.Faces.Count - 1);
            Assert.Empty(resolution.Diagnostics);
        });
    }

    [Fact]
    public void OpeningOffsetWithinDocumentToleranceResolvesTheSameHostFace()
    {
        RunWithNativeGeometry(() =>
        {
            const double tolerance = 0.01d;
            using Brep zone = ZoneBrep();
            using PolylineCurve coplanar = OpeningCurve(y: 0d);
            using PolylineCurve nearPlane = OpeningCurve(y: tolerance * 0.5d);

            Resolution expected = Resolve(zone, coplanar, tolerance);
            Resolution actual = Resolve(zone, nearPlane, tolerance);

            Assert.True(expected.IsSuccess);
            Assert.True(actual.IsSuccess);
            Assert.Equal(expected.FaceIndex, actual.FaceIndex);
            Assert.Empty(actual.Diagnostics);
        });
    }

    [Fact]
    public void CoplanarOpeningOutsideEveryFaceReportsHostNotFound()
    {
        RunWithNativeGeometry(() =>
        {
            var openingId = new EntityId("OPENING-NO-HOST");
            using Brep zone = ZoneBrep();
            using PolylineCurve opening = OpeningCurve(y: 0d, xOffset: 9d);

            Resolution resolution = Resolve(
                zone,
                opening,
                absoluteTolerance: 0.001d,
                openingId: openingId);

            Assert.False(resolution.IsSuccess);
            Assert.Null(resolution.FaceIndex);
            Diagnostic diagnostic = Assert.Single(resolution.Diagnostics);
            Assert.Equal("SD.GH.OPENING_HOST_NOT_FOUND", diagnostic.Code);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(openingId, diagnostic.ObjectId);
            Assert.NotNull(diagnostic.SuggestedAction);
        });
    }

    [Fact]
    public void CoincidentDuplicateZoneShellsReportAmbiguousHost()
    {
        RunWithNativeGeometry(() =>
        {
            var openingId = new EntityId("OPENING-AMBIGUOUS-HOST");
            using Brep firstShell = ZoneBrep();
            using Brep secondShell = firstShell.DuplicateBrep();
            using var zoneWithDuplicateShells = new Brep();
            zoneWithDuplicateShells.Append(firstShell);
            zoneWithDuplicateShells.Append(secondShell);
            using PolylineCurve opening = OpeningCurve(y: 0d);

            Resolution resolution = Resolve(
                zoneWithDuplicateShells,
                opening,
                absoluteTolerance: 0.001d,
                openingId: openingId);

            Assert.False(resolution.IsSuccess);
            Assert.Null(resolution.FaceIndex);
            Diagnostic diagnostic = Assert.Single(resolution.Diagnostics);
            Assert.Equal("SD.GH.OPENING_HOST_AMBIGUOUS", diagnostic.Code);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(openingId, diagnostic.ObjectId);
            Assert.NotNull(diagnostic.SuggestedAction);
        });
    }

    private static Resolution Resolve(
        Brep zone,
        Curve opening,
        double absoluteTolerance,
        EntityId? openingId = null)
    {
        Type resolverType = Assert.IsAssignableFrom<Type>(LoadPlugin().GetType(ResolverTypeName));
        MethodInfo resolve = Assert.IsAssignableFrom<MethodInfo>(resolverType.GetMethod(
            "Resolve",
            BindingFlags.Static | BindingFlags.NonPublic));
        Type contextType = resolve.GetParameters()[2].ParameterType;
        object context = Assert.IsAssignableFrom<object>(Activator.CreateInstance(
            contextType,
            UnitSystem.Meters,
            absoluteTolerance,
            RhinoMath.DefaultAngleTolerance));
        object result = Assert.IsAssignableFrom<object>(resolve.Invoke(
            null,
            new object?[] { zone, opening, context, openingId }));
        Type resultType = result.GetType();

        object? faceIndexValue = Assert.IsAssignableFrom<PropertyInfo>(resultType.GetProperty(
            "FaceIndex",
            BindingFlags.Instance | BindingFlags.NonPublic)).GetValue(result);
        int? faceIndex = faceIndexValue is null ? null : Assert.IsType<int>(faceIndexValue);
        IReadOnlyList<Diagnostic> diagnostics = Assert.IsAssignableFrom<IReadOnlyList<Diagnostic>>(
            Assert.IsAssignableFrom<PropertyInfo>(resultType.GetProperty(
                "Diagnostics",
                BindingFlags.Instance | BindingFlags.NonPublic)).GetValue(result));
        bool isSuccess = Assert.IsType<bool>(Assert.IsAssignableFrom<PropertyInfo>(resultType.GetProperty(
            "IsSuccess",
            BindingFlags.Instance | BindingFlags.NonPublic)).GetValue(result));

        return new Resolution(faceIndex, diagnostics, isSuccess);
    }

    private static PolylineCurve OpeningCurve(double y, double xOffset = 0d)
    {
        return new PolylineCurve(new[]
        {
            new Point3d(xOffset + 2d, y, 0.8d),
            new Point3d(xOffset + 5d, y, 0.8d),
            new Point3d(xOffset + 5d, y, 2.2d),
            new Point3d(xOffset + 2d, y, 2.2d),
            new Point3d(xOffset + 2d, y, 0.8d),
        });
    }

    private static Brep ZoneBrep()
    {
        return new Box(
            Plane.WorldXY,
            new Interval(0d, 8d),
            new Interval(0d, 6d),
            new Interval(0d, 3d)).ToBrep();
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

    private static void RunWithNativeGeometry(Action assertion)
    {
        try
        {
            assertion();
        }
        catch (DllNotFoundException exception)
        {
            // Managed CI does not load Rhino's native geometry kernel. These
            // assertions execute in Rhino-hosted runs and mirror the existing
            // geometry-backed authoring contract tests.
            Assert.Contains("rhcommon_c", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record Resolution(
        int? FaceIndex,
        IReadOnlyList<Diagnostic> Diagnostics,
        bool IsSuccess);
}
