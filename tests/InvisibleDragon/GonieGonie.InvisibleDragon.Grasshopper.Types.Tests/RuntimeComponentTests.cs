using System.Reflection;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace GonieGonie.InvisibleDragon.Grasshopper.Tests;

public sealed class RuntimeComponentTests
{
    private static readonly string[] CanonicalCompileInputNames = { "Model" };

    private static readonly string[] CanonicalCompileInputTypes = { "DragonEnergyModelParam" };

    private static readonly string[] CanonicalCompileOutputNames =
    {
        "IDF",
        "Text",
        "Valid",
        "Diagnostics",
    };

    private static readonly string[] ManagedRunInputNames =
    {
        "IDF",
        "Weather",
        "Run",
        "Cancel",
        "Force Rerun",
        "Timeout",
    };

    private static readonly string[] ManagedRunInputTypes =
    {
        "DragonIdfParam",
        "PreparedWeatherFileParam",
        "Param_Boolean",
        "Param_Boolean",
        "Param_Boolean",
        "Param_Number",
    };

    private static readonly string[] ManagedRunOutputNames =
    {
        "Result",
        "State",
        "Success",
        "Diagnostics",
    };

    private static readonly string[] WeatherInputNames = { "EPW File" };

    private static readonly string[] WeatherInputTypes = { "Param_String" };

    private static readonly string[] WeatherOutputNames =
    {
        "Weather",
        "Success",
        "Diagnostics",
    };

    private static readonly string[] WeatherOutputTypes =
    {
        "PreparedWeatherFileParam",
        "Param_Boolean",
        "DiagnosticParam",
    };

    [Fact]
    public void ExplicitTriggerGateRequiresANewRisingEdgeAfterDocumentLoad()
    {
        Assembly assembly = LoadPlugin();
        Type gateType = assembly.GetType(
            "GonieGonie.InvisibleDragon.Grasshopper.Components.ExplicitTriggerGate",
            throwOnError: true)!;
        object gate = Activator.CreateInstance(gateType, nonPublic: true)!;
        MethodInfo observe = gateType.GetMethod(
            "Observe",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo reset = gateType.GetMethod(
            "Reset",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        AssertEdges(Observe(observe, gate, start: true, cancel: true), start: false, cancel: false);
        AssertEdges(Observe(observe, gate, start: true, cancel: true), start: false, cancel: false);
        AssertEdges(Observe(observe, gate, start: false, cancel: false), start: false, cancel: false);
        AssertEdges(Observe(observe, gate, start: true, cancel: true), start: true, cancel: true);
        AssertEdges(Observe(observe, gate, start: true, cancel: true), start: false, cancel: false);

        reset.Invoke(gate, null);
        AssertEdges(Observe(observe, gate, start: true, cancel: false), start: false, cancel: false);
        AssertEdges(Observe(observe, gate, start: false, cancel: false), start: false, cancel: false);
        AssertEdges(Observe(observe, gate, start: true, cancel: false), start: true, cancel: false);
    }

    [Fact]
    public void CanonicalCompileUsesNoUserManagedIddPath()
    {
        Assembly assembly = LoadPlugin();
        GH_Component component = CreateComponent(
            assembly,
            "GonieGonie.InvisibleDragon.Grasshopper.Components.CompileInvisibleDragonComponent");

        Assert.Equal(new Guid("e3e4d8f9-4fd8-4b17-9ec7-a27cb5627802"), component.ComponentGuid);
        Assert.Equal("Compile InvisibleDragon", component.Name);
        Assert.Equal(GH_Exposure.primary, component.Exposure);
        Assert.Equal(CanonicalCompileInputNames, component.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(CanonicalCompileInputTypes, component.Params.Input.Select(parameter => parameter.GetType().Name));
        Assert.Equal(
            CanonicalCompileOutputNames,
            component.Params.Output.Select(parameter => parameter.Name));
        Assert.DoesNotContain(
            component.Params.Input,
            parameter => parameter.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("Root", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("Directory", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("resolved internally", component.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManagedRunConsumesTypedWeatherAndExposesNoSetupPaths()
    {
        Assembly assembly = LoadPlugin();
        GH_Component component = CreateComponent(
            assembly,
            "GonieGonie.InvisibleDragon.Grasshopper.Components.ManagedRunEnergyPlusComponent");

        Assert.Equal(new Guid("50e4f5bf-f174-458f-bfaa-aaf4e25ce5b5"), component.ComponentGuid);
        Assert.Equal("Run InvisibleDragon", component.Name);
        Assert.Equal(GH_Exposure.primary, component.Exposure);
        Assert.Equal(ManagedRunInputNames, component.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(ManagedRunInputTypes, component.Params.Input.Select(parameter => parameter.GetType().Name));
        Assert.Equal(ManagedRunOutputNames, component.Params.Output.Select(parameter => parameter.Name));
        Assert.DoesNotContain(
            component.Params.Input,
            parameter => parameter.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("Root", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("Directory", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("managed internally", component.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ID Weather", component.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("SimpleDragon", component.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            component.Params.Input,
            parameter => parameter.Description.Contains(
                "SimpleDragon",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(false, PersistentDefault(component.Params.Input[2]));
    }

    [Fact]
    public void ManagedRunRejectsMultipleDataMatchedInputSets()
    {
        Assembly assembly = LoadPlugin();
        GH_Component component = CreateComponent(
            assembly,
            "GonieGonie.InvisibleDragon.Grasshopper.Components.ManagedRunEnergyPlusComponent");
        Assert.True(component.Params.Input[2].AddVolatileData(
            new GH_Path(2),
            0,
            new GH_Boolean(false)));
        Assert.True(component.Params.Input[2].AddVolatileData(
            new GH_Path(5),
            0,
            new GH_Boolean(false)));

        MethodInfo beforeSolve = Assert.IsAssignableFrom<MethodInfo>(component.GetType().GetMethod(
            "BeforeSolveInstance",
            BindingFlags.Instance | BindingFlags.NonPublic));
        beforeSolve.Invoke(component, null);

        string error = Assert.Single(component.RuntimeMessages(GH_RuntimeMessageLevel.Error));
        Assert.Contains("one data-matched input set", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one Run InvisibleDragon component", error, StringComparison.Ordinal);
    }

    [Fact]
    public void InvisibleDragonWeatherHasOneDeliberateFileInputAndTypedOutputs()
    {
        Assembly assembly = LoadPlugin();
        GH_Component component = CreateComponent(
            assembly,
            "GonieGonie.InvisibleDragon.Grasshopper.Components.VerifyInvisibleDragonWeatherComponent");

        Assert.Equal(new Guid("4f443564-2e13-4a79-8845-27d1e6eb285d"), component.ComponentGuid);
        Assert.Equal("Verify InvisibleDragon Weather", component.Name);
        Assert.Equal("ID Weather", component.NickName);
        Assert.Equal("InvisibleDragon", component.Category);
        Assert.Equal("Core", component.SubCategory);
        Assert.Equal(GH_Exposure.primary, component.Exposure);
        Assert.Equal(WeatherInputNames, component.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(WeatherInputTypes, component.Params.Input.Select(parameter => parameter.GetType().Name));
        Assert.True(component.Params.Input[0].Optional);
        Assert.Equal(WeatherOutputNames, component.Params.Output.Select(parameter => parameter.Name));
        Assert.Equal(WeatherOutputTypes, component.Params.Output.Select(parameter => parameter.GetType().Name));
        Assert.DoesNotContain(
            component.Params.Input,
            parameter => parameter.Name.Contains("Runtime", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("Root", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("Directory", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("IDD", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            component.Params.Output,
            parameter => parameter.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("File", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("Directory", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("paths are not exposed", component.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SimpleDragon", component.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManagedRunNormalizesRuntimeFailureTextBeforeCreatingDiagnostics()
    {
        Assembly assembly = LoadPlugin();
        Type componentType = assembly.GetType(
            "GonieGonie.InvisibleDragon.Grasshopper.Components.ManagedRunEnergyPlusComponent",
            throwOnError: true)!;
        Type outcomeType = Assert.Single(
            componentType.GetNestedTypes(BindingFlags.NonPublic),
            type => string.Equals(type.Name, "RunOutcome", StringComparison.Ordinal));
        MethodInfo fromFailure = Assert.IsAssignableFrom<MethodInfo>(outcomeType.GetMethod(
            "FromFailure",
            BindingFlags.Static | BindingFlags.NonPublic));
        var failure = new EnergyPlusFailure(
            EnergyPlusFailureCategory.RuntimeEnvironment,
            "WHITESPACE_CONTRACT",
            "  Runtime failed.  ",
            "  Retry the managed runtime.  ");

        object? rawOutcome = fromFailure.Invoke(null, new object[] { failure });
        Assert.NotNull(rawOutcome);
        PropertyInfo diagnosticsProperty = Assert.IsAssignableFrom<PropertyInfo>(outcomeType.GetProperty(
            "Diagnostics",
            BindingFlags.Instance | BindingFlags.NonPublic));
        IReadOnlyList<Diagnostic> diagnostics = Assert.IsAssignableFrom<IReadOnlyList<Diagnostic>>(
            diagnosticsProperty.GetValue(rawOutcome));
        Diagnostic diagnostic = Assert.Single(diagnostics);

        Assert.Equal("Runtime failed.", diagnostic.Message);
        Assert.Equal("Retry the managed runtime.", diagnostic.SuggestedAction);
    }

    [Fact]
    public void ManagedRunInternalFailureDoesNotExposeImplementationPaths()
    {
        Assembly assembly = LoadPlugin();
        Type componentType = assembly.GetType(
            "GonieGonie.InvisibleDragon.Grasshopper.Components.ManagedRunEnergyPlusComponent",
            throwOnError: true)!;
        Type outcomeType = Assert.Single(
            componentType.GetNestedTypes(BindingFlags.NonPublic),
            type => string.Equals(type.Name, "RunOutcome", StringComparison.Ordinal));
        MethodInfo internalFailure = Assert.IsAssignableFrom<MethodInfo>(outcomeType.GetMethod(
            "InternalFailure",
            BindingFlags.Static | BindingFlags.NonPublic));
        const string privatePath = @"C:\private\runtime\EnergyPlus.exe";

        object? rawOutcome = internalFailure.Invoke(null, new object[] { new IOException(privatePath) });
        Assert.NotNull(rawOutcome);
        PropertyInfo diagnosticsProperty = Assert.IsAssignableFrom<PropertyInfo>(outcomeType.GetProperty(
            "Diagnostics",
            BindingFlags.Instance | BindingFlags.NonPublic));
        IReadOnlyList<Diagnostic> diagnostics = Assert.IsAssignableFrom<IReadOnlyList<Diagnostic>>(
            diagnosticsProperty.GetValue(rawOutcome));
        Diagnostic diagnostic = Assert.Single(diagnostics);

        Assert.DoesNotContain(privatePath, diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(diagnostic.SuggestedAction);
    }

    private static object Observe(MethodInfo method, object gate, bool start, bool cancel)
    {
        return method.Invoke(gate, new object[] { start, cancel })!;
    }

    private static void AssertEdges(object observation, bool start, bool cancel)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = observation.GetType();
        Assert.Equal(start, (bool)type.GetProperty("Start", flags)!.GetValue(observation)!);
        Assert.Equal(cancel, (bool)type.GetProperty("Cancel", flags)!.GetValue(observation)!);
    }

    private static object? PersistentDefault(IGH_Param parameter)
    {
        PropertyInfo? persistentData = parameter.GetType().GetProperty("PersistentData");
        object? structure = persistentData?.GetValue(parameter);
        MethodInfo? allData = structure?.GetType().GetMethod(
            "AllData",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(bool) },
            modifiers: null);
        var data = allData?.Invoke(structure, new object[] { true }) as System.Collections.IEnumerable;
        object? first = data?.Cast<object>().FirstOrDefault();
        return first is IGH_Goo goo ? goo.ScriptVariable() : first;
    }

    private static GH_Component CreateComponent(Assembly assembly, string typeName)
    {
        Type type = assembly.GetType(typeName, throwOnError: true)!;
        return Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
    }

    private static Assembly LoadPlugin()
    {
        string repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        string path = Path.Combine(
            repositoryRoot,
            "temp",
            "build",
            "bin",
            "GonieGonie.InvisibleDragon.GH",
            "Release",
            "net8.0-windows",
            "GonieGonie.InvisibleDragon.GH.gha");
        Assert.True(File.Exists(path), "Expected built Grasshopper assembly at '" + path + "'.");
        return Assembly.LoadFrom(path);
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

        throw new DirectoryNotFoundException("Could not locate the Dragons.Grasshopper.sln repository root.");
    }
}
