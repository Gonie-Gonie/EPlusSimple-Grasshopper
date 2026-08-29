using System.Reflection;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using Grasshopper.Kernel;
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

    private static readonly string[] PrepareInputNames =
    {
        "Target Root",
        "Prepare",
        "Cancel",
        "Replace Invalid Custom Target",
        "Lock Timeout",
    };

    private static readonly string[] PrepareOutputNames =
    {
        "Runtime Root",
        "Executable",
        "State",
        "Ready",
        "Progress",
        "Message",
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
    public void PrepareRuntimeComponentMakesBundledFirstAcquisitionAndSafeCacheExplicit()
    {
        Assembly assembly = LoadPlugin();
        GH_Component component = CreateComponent(
            assembly,
            "GonieGonie.InvisibleDragon.Grasshopper.Components.PrepareEnergyPlusRuntimeComponent");

        Assert.Equal(new Guid("5199b03c-644b-4194-b38c-37f3c7a423aa"), component.ComponentGuid);
        Assert.Equal("InvisibleDragon", component.Category);
        Assert.Contains("bundled archive is used first", component.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LocalAppData", component.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("weather files are never acquired", component.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PrepareInputNames, component.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(PrepareOutputNames, component.Params.Output.Select(parameter => parameter.Name));
        Assert.Contains("saved True value does not run", component.Params.Input[1].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("False to True", component.Params.Input[2].Description, StringComparison.Ordinal);
        Assert.Contains("Bundled-copy", component.Params.Output[4].Description, StringComparison.Ordinal);
        Assert.Contains("0 to 1", component.Params.Output[4].Description, StringComparison.Ordinal);
        Assert.Equal(false, PersistentDefault(component.Params.Input[1]));
    }

    [Fact]
    public void RunComponentOnlyBootstrapsAsPartOfAnExplicitRunRequest()
    {
        Assembly assembly = LoadPlugin();
        GH_Component component = CreateComponent(
            assembly,
            "GonieGonie.InvisibleDragon.Grasshopper.Components.RunEnergyPlusComponent");

        Assert.Equal(10, component.Params.Input.Count);
        Assert.Equal("Prepare Missing Runtime", component.Params.Input[9].Name);
        Assert.Contains("When Run rises", component.Params.Input[9].Description, StringComparison.Ordinal);
        Assert.Contains("bundled archive is used first", component.Params.Input[9].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LocalAppData", component.Params.Input[9].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never acquires weather", component.Params.Input[9].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("user-supplied EPW", component.Params.Input[1].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(true, PersistentDefault(component.Params.Input[9]));

        Type gateType = assembly.GetType(
            "GonieGonie.InvisibleDragon.Grasshopper.Components.ExplicitTriggerGate",
            throwOnError: true)!;
        foreach (string componentTypeName in new[]
        {
            "GonieGonie.InvisibleDragon.Grasshopper.Components.PrepareEnergyPlusRuntimeComponent",
            "GonieGonie.InvisibleDragon.Grasshopper.Components.RunEnergyPlusComponent",
        })
        {
            Type componentType = assembly.GetType(componentTypeName, throwOnError: true)!;
            FieldInfo field = componentType.GetField(
                "triggerGate",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.Equal(gateType, field.FieldType);
        }
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
        Assert.Contains("only consumes", component.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(false, PersistentDefault(component.Params.Input[2]));

        GH_Component legacyRun = CreateComponent(
            assembly,
            "GonieGonie.InvisibleDragon.Grasshopper.Components.RunEnergyPlusComponent");
        GH_Component legacyPrepare = CreateComponent(
            assembly,
            "GonieGonie.InvisibleDragon.Grasshopper.Components.PrepareEnergyPlusRuntimeComponent");
        GH_Component legacyCompile = CreateComponent(
            assembly,
            "GonieGonie.InvisibleDragon.Grasshopper.Components.CompileIdfComponent");
        GH_Component legacyValidate = CreateComponent(
            assembly,
            "GonieGonie.InvisibleDragon.Grasshopper.Components.ValidateIdfComponent");
        Assert.Equal(GH_Exposure.hidden, legacyRun.Exposure);
        Assert.Equal(GH_Exposure.hidden, legacyPrepare.Exposure);
        Assert.Equal(GH_Exposure.hidden, legacyCompile.Exposure);
        Assert.Equal(GH_Exposure.hidden, legacyValidate.Exposure);
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
