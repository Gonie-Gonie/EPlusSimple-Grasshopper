using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

[SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Inline arrays keep each Grasshopper port-contract assertion local and readable.")]
public sealed class ManagedBatchComponentContractTests
{
    private const string ManagedTypeName =
        "GonieGonie.SimpleDragon.Grasshopper.Components.ManagedRunSimpleDragonBatchComponent";

    [Fact]
    public void ManagedBatchHasStablePathlessTypedContract()
    {
        GH_Component component = Component(ManagedTypeName);

        Assert.Equal(new Guid("e0a54494-3d69-4681-8756-cc3cd86df4e1"), component.ComponentGuid);
        Assert.Equal(GH_Exposure.primary, component.Exposure);
        Assert.Equal(
            new[] { "Cases", "Parallel Limit", "Run", "Cancel" },
            component.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(
            new[] { "State", "Case IDs", "Statuses", "Combined CSV", "Manifest", "Complete", "Diagnostics" },
            component.Params.Output.Select(parameter => parameter.Name));
        Assert.IsType<SimpleDragonBatchCaseParam>(component.Params.Input[0]);
        Assert.Equal(GH_ParamAccess.list, component.Params.Input[0].Access);
        Assert.Equal(GH_ParamAccess.item, component.Params.Input[1].Access);
        Assert.Equal(GH_ParamAccess.item, component.Params.Input[2].Access);
        Assert.Equal(GH_ParamAccess.item, component.Params.Input[3].Access);
        Assert.All(
            component.Params.Input,
            parameter => Assert.DoesNotContain(
                new[] { "Path", "Root", "EPW", "IDD", "Runtime", "Output", "Temp" },
                forbidden => parameter.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ManagedDirectoriesAndResolversAreConfinedToOsTempDragonRoot()
    {
        Type componentType = ManagedComponentType();
        Type pathsType = Assert.Single(
            componentType.GetNestedTypes(BindingFlags.NonPublic),
            type => type.Name == "ManagedBatchPaths");
        MethodInfo create = Assert.IsAssignableFrom<MethodInfo>(pathsType.GetMethod(
            "Create",
            BindingFlags.Static | BindingFlags.NonPublic));
        string tempDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "managed-batch-contract-root"));
        object paths = Required(create.Invoke(null, new object[] { tempDirectory }));
        Assert.Equal(pathsType, paths.GetType());
        string expectedRoot = Path.Combine(tempDirectory, "GonieGonie", "Dragons");
        string root = Property<string>(paths, "Root");
        string runtimeRoot = Property<string>(paths, "RuntimeRoot");
        string weatherRoot = Property<string>(paths, "WeatherCacheRoot");
        string outputRoot = Property<string>(paths, "OutputRoot");

        Assert.Equal(expectedRoot, root);
        AssertChild(root, runtimeRoot);
        AssertChild(root, weatherRoot);
        AssertChild(root, outputRoot);

        object resolveOptions = InvokeStatic(componentType, "CreateRuntimeResolveOptions", paths);
        Assert.Equal(runtimeRoot, Property<string>(resolveOptions, "RuntimeRoot"));
        Assert.False(Property<bool>(resolveOptions, "SearchEnvironmentVariables"));
        Assert.False(Property<bool>(resolveOptions, "SearchDefaultCacheLocation"));
        Assert.False(Property<bool>(resolveOptions, "SearchDefaultInstallLocation"));

        object bootstrapOptions = InvokeStatic(componentType, "CreateRuntimeBootstrapOptions", paths);
        Assert.Equal(runtimeRoot, Property<string>(bootstrapOptions, "TargetRoot"));
        Assert.True(Property<bool>(bootstrapOptions, "ReplaceInvalidExistingTarget"));

        object weatherOptions = InvokeStatic(componentType, "CreateWeatherPackOptions", weatherRoot);
        Assert.Equal(weatherRoot, Property<string>(weatherOptions, "CacheRoot"));
    }

    [Fact]
    public void AddressOnlyModelIsAcceptedForInternalWeatherSelection()
    {
        Type componentType = ManagedComponentType();
        GH_Component component = Component(ManagedTypeName);
        GreenRetrofitModel model = AddressOnlyModel();
        MethodInfo method = Assert.IsAssignableFrom<MethodInfo>(componentType.GetMethod(
            "TryCreateInputs",
            BindingFlags.Instance | BindingFlags.NonPublic));

        object? inputs = method.Invoke(
            component,
            new object[]
            {
                new List<SimpleDragonBatchCaseGoo>
                {
                    new(new SimpleDragonBatchCase(model, "address-model")),
                },
                2,
            });

        Assert.NotNull(inputs);
        Assert.Equal(2, Property<int>(inputs, "MaxDegreeOfParallelism"));
        object cases = Property<object>(inputs, "Cases");
        Assert.Single(Assert.IsAssignableFrom<System.Collections.IEnumerable>(cases).Cast<object>());

        object? excessiveParallelism = method.Invoke(
            component,
            new object[]
            {
                new List<SimpleDragonBatchCaseGoo>
                {
                    new(new SimpleDragonBatchCase(model, "address-model")),
                },
                1025,
            });
        Assert.Null(excessiveParallelism);
    }

    [Fact]
    public void TriggerAndCancellationRemainExplicitAndPathFree()
    {
        Type componentType = ManagedComponentType();
        Type gateType = Assert.Single(
            componentType.GetNestedTypes(BindingFlags.NonPublic),
            type => type.Name == "ExplicitManagedBatchTriggerGate");
        object gate = Required(Activator.CreateInstance(gateType, nonPublic: true));
        Assert.Equal(gateType, gate.GetType());
        MethodInfo observe = Assert.IsAssignableFrom<MethodInfo>(gateType.GetMethod(
            "Observe",
            BindingFlags.Instance | BindingFlags.NonPublic));

        AssertObservation(observe.Invoke(gate, new object[] { true, true }), start: false, cancel: false);
        AssertObservation(observe.Invoke(gate, new object[] { false, false }), start: false, cancel: false);
        AssertObservation(observe.Invoke(gate, new object[] { true, false }), start: true, cancel: false);
        AssertObservation(observe.Invoke(gate, new object[] { true, true }), start: false, cancel: true);

        Type outcomeType = Assert.Single(
            componentType.GetNestedTypes(BindingFlags.NonPublic),
            type => type.Name == "ManagedBatchOutcome");
        object cancelled = Required(
            Assert.IsAssignableFrom<MethodInfo>(outcomeType.GetMethod(
                "Cancelled",
                BindingFlags.Static | BindingFlags.NonPublic)).Invoke(null, null));
        Assert.Equal(outcomeType, cancelled.GetType());
        Assert.Equal("Cancelled", Property<string>(cancelled, "State"));
        Diagnostic cancelDiagnostic = Assert.Single(Property<IReadOnlyList<Diagnostic>>(cancelled, "Diagnostics"));
        Assert.Equal("SD.GH.MANAGED_BATCH_CANCELLED", cancelDiagnostic.Code);
        Assert.False(cancelDiagnostic.IsFailure);

        const string privatePath = @"C:\private\runtime\EnergyPlus.exe";
        object failed = Required(
            Assert.IsAssignableFrom<MethodInfo>(outcomeType.GetMethod(
                "InternalFailure",
                BindingFlags.Static | BindingFlags.NonPublic)).Invoke(
                    null,
                    new object[] { new IOException(privatePath) }));
        Assert.Equal(outcomeType, failed.GetType());
        Diagnostic failure = Assert.Single(Property<IReadOnlyList<Diagnostic>>(failed, "Diagnostics"));
        Assert.DoesNotContain(privatePath, failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(failure.SuggestedAction);
    }

    private static GreenRetrofitModel AddressOnlyModel()
    {
        WeatherMetadata metadata = SimpleDragonDatabase.Default.Weather.Items[0];
        return new GreenRetrofitModel(
            "Managed Batch Address Model",
            0,
            metadata.AdministrativeArea,
            new DateTime(2020, 1, 1),
            false,
            Array.Empty<BuildingFloor>(),
            Array.Empty<Material>(),
            Array.Empty<SurfaceConstruction>(),
            Array.Empty<FenestrationConstruction>());
    }

    private static object InvokeStatic(Type type, string methodName, object argument)
    {
        MethodInfo method = Assert.IsAssignableFrom<MethodInfo>(type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic));
        return Required(method.Invoke(null, new[] { argument }));
    }

    private static T Property<T>(object value, string name)
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(value.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        return Assert.IsAssignableFrom<T>(property.GetValue(value));
    }

    private static void AssertChild(string root, string candidate)
    {
        Assert.StartsWith(
            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar,
            candidate,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertObservation(object? observation, bool start, bool cancel)
    {
        object value = Required(observation);
        Assert.Equal(start, Property<bool>(value, "Start"));
        Assert.Equal(cancel, Property<bool>(value, "Cancel"));
    }

    private static GH_Component Component(string typeName)
    {
        Type type = Assert.IsAssignableFrom<Type>(LoadPlugin().GetType(typeName));
        return Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
    }

    private static Type ManagedComponentType()
    {
        return Assert.IsAssignableFrom<Type>(LoadPlugin().GetType(ManagedTypeName));
    }

    private static object Required(object? value)
    {
        Assert.NotNull(value);
        return value!;
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
