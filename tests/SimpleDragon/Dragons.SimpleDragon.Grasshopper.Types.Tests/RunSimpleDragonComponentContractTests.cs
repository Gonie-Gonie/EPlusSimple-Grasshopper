using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.EnergyPlus.Runtime;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;

namespace Dragons.SimpleDragon.Grasshopper.Tests;

[SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Inline arrays keep the direct Grasshopper contract readable.")]
public sealed class RunSimpleDragonComponentContractTests
{
    private const string ComponentTypeName =
        "Dragons.SimpleDragon.Grasshopper.Components.RunSimpleDragonComponent";

    [Fact]
    public void DirectRunHasStableSimpleDragonContractWithOneOptionalResultPath()
    {
        GH_Component component = Component();

        Assert.Equal(new Guid("6e242e51-77ce-4f77-8445-a17d636c7310"), component.ComponentGuid);
        Assert.Equal("Run SimpleDragon", component.Name);
        Assert.Equal("SD Run", component.NickName);
        Assert.Equal(GH_Exposure.primary, component.Exposure);
        Assert.DoesNotContain("IDF", component.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "InvisibleDragon",
            component.Description,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            new[] { "GRM", "Run", "Cancel", "Force Rerun", "Timeout", "GRR Path" },
            component.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(
            new[] { "GRR", "State", "Success", "Diagnostics" },
            component.Params.Output.Select(parameter => parameter.Name));
        Assert.Equal("GreenRetrofitModelParam", component.Params.Input[0].GetType().Name);
        Assert.IsType<Param_String>(component.Params.Input[5]);
        Assert.Equal(GH_ParamAccess.item, component.Params.Input[5].Access);
        Assert.True(component.Params.Input[5].Optional);
        Assert.Null(PersistentDefault(component.Params.Input[5]));
        Assert.Contains("Leave blank", component.Params.Input[5].Description, StringComparison.Ordinal);
        Assert.Equal("GreenRetrofitResultParam", component.Params.Output[0].GetType().Name);
        Assert.Equal("SimpleDragonDiagnosticParam", component.Params.Output[3].GetType().Name);
        Assert.Equal(GH_ParamAccess.list, component.Params.Output[3].Access);
        Assert.False(Assert.IsType<bool>(PersistentDefault(component.Params.Input[1])));
        Assert.False(Assert.IsType<bool>(PersistentDefault(component.Params.Input[2])));
        Assert.False(Assert.IsType<bool>(PersistentDefault(component.Params.Input[3])));
        Assert.Equal(30d, Assert.IsType<double>(PersistentDefault(component.Params.Input[4])));

        Assert.Equal(
            new[] { "GRR Path" },
            component.Params.Input
                .Where(parameter => parameter.Name.Contains("Path", StringComparison.OrdinalIgnoreCase))
                .Select(parameter => parameter.Name));
        Assert.All(component.Params.Input, parameter =>
        {
            Assert.DoesNotContain("Directory", parameter.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Root", parameter.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("EPW", parameter.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("IDD", parameter.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("IDF", parameter.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("EnergyPlus", parameter.Name, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void BlankGrrPathSkipsResolutionAndRelativePathUsesUnsavedDocumentTemp()
    {
        GH_Component component = Component();
        MethodInfo resolve = Method(
            component.GetType(),
            "ResolveOptionalGrrPath",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Null(resolve.Invoke(component, new object[] { string.Empty }));
        Assert.Null(resolve.Invoke(component, new object[] { "   " }));

        string relative = Path.Combine("dragon-tests", Guid.NewGuid().ToString("N"), "result.grr");
        string resolved = Assert.IsType<string>(resolve.Invoke(component, new object[] { relative }));
        Assert.Equal(
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), relative)),
            resolved,
            ignoreCase: true);
    }

    [Fact]
    public void OptionalGrrPersistenceSkipsBlankCreatesParentsAndPreservesResultOnWriteFailure()
    {
        Type componentType = ComponentType();
        MethodInfo persist = Method(
            componentType,
            "PersistResult",
            BindingFlags.Static | BindingFlags.NonPublic);
        GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(
            12d,
            EnergyUseBreakdown.Create((_, _) => Enumerable.Repeat(1d, MonthlySeries.MonthCount)));
        string root = Path.Combine(Path.GetTempPath(), "dragon-grr-run-tests", Guid.NewGuid().ToString("N"));

        try
        {
            object skipped = Required(persist.Invoke(
                null,
                new object?[]
                {
                    result,
                    "Succeeded",
                    Array.Empty<Diagnostic>(),
                    "   ",
                    CancellationToken.None,
                }));
            Assert.True(Property<bool>(skipped, "Success"));
            Assert.Equal("Succeeded", Property<string>(skipped, "State"));
            Assert.False(Directory.Exists(root));

            string destination = Path.Combine(root, "nested", "result.grr");
            object written = Required(persist.Invoke(
                null,
                new object?[]
                {
                    result,
                    "Succeeded",
                    Array.Empty<Diagnostic>(),
                    destination,
                    CancellationToken.None,
                }));
            Assert.True(Property<bool>(written, "Success"));
            Assert.Same(result, Property<GreenRetrofitResult>(written, "Result"));
            Assert.Equal("Succeeded", Property<string>(written, "State"));
            Assert.True(File.Exists(destination));
            byte[] bytes = File.ReadAllBytes(destination);
            Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.Equal(GrrWriter.Serialize(result), File.ReadAllText(destination));
            Assert.True(GrrReader.ReadFile(destination).Success);

            object failed = Required(persist.Invoke(
                null,
                new object?[]
                {
                    result,
                    "Cached",
                    Array.Empty<Diagnostic>(),
                    root,
                    CancellationToken.None,
                }));
            Assert.True(Property<bool>(failed, "Success"));
            Assert.Same(result, Property<GreenRetrofitResult>(failed, "Result"));
            Assert.Equal("GRR Save Failed", Property<string>(failed, "State"));
            Diagnostic diagnostic = Assert.Single(Property<IReadOnlyList<Diagnostic>>(failed, "Diagnostics"));
            Assert.Equal("SD.GH.RUN_GRR_WRITE_FAILED", diagnostic.Code);
            Assert.True(diagnostic.IsFailure);
            Assert.DoesNotContain(root, diagnostic.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                root,
                diagnostic.SuggestedAction ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void DirectRunRejectsMultipleDataMatchedInputSets()
    {
        GH_Component component = Component();
        Assert.True(component.Params.Input[1].AddVolatileData(
            new GH_Path(2),
            0,
            new GH_Boolean(false)));
        Assert.True(component.Params.Input[1].AddVolatileData(
            new GH_Path(5),
            0,
            new GH_Boolean(false)));

        Method(
                component.GetType(),
                "BeforeSolveInstance",
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(component, null);

        string error = Assert.Single(component.RuntimeMessages(GH_RuntimeMessageLevel.Error));
        Assert.Contains("one data-matched input set", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Managed Run SimpleDragon Batch", error, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectRunMapsCoreProgressToNeutralSimpleDragonStates()
    {
        MethodInfo state = Method(
            ComponentType(),
            "UserFacingState",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.Equal(
            "Preparing Model",
            Assert.IsType<string>(state.Invoke(
                null,
                new object[] { SimpleDragonSimulationState.ConvertingModel })));
        Assert.Equal(
            "Preparing Simulation",
            Assert.IsType<string>(state.Invoke(
                null,
                new object[] { SimpleDragonSimulationState.CompilingIdf })));

        foreach (SimpleDragonSimulationState value in Enum.GetValues(typeof(SimpleDragonSimulationState)))
        {
            string visible = Assert.IsType<string>(state.Invoke(null, new object[] { value }));
            Assert.DoesNotContain("IDF", visible, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("InvisibleDragon", visible, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SimulationFailureRedactsRetainedWorkDirectoryAtDirectRunBoundary()
    {
        const string privatePath = @"C:\Users\private-user\AppData\Local\Temp\run-123";
        var source = new Diagnostic(
            "ENERGYPLUS.RUNTIME.RUN_FAILED",
            DiagnosticSeverity.Error,
            "EnergyPlus execution failed.",
            new EntityId("ZONE-PATH-BOUNDARY"),
            suggestedAction: "Review the error output. Retained work directory: " + privatePath);

        Diagnostic visible = BoundaryDiagnostic(source);

        Assert.Equal(source.Code, visible.Code);
        Assert.Equal(source.Severity, visible.Severity);
        Assert.Equal(source.ObjectId, visible.ObjectId);
        Assert.Equal(source.Message, visible.Message);
        Assert.DoesNotContain(privatePath, visible.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            privatePath,
            visible.SuggestedAction ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Retained work directory",
            visible.SuggestedAction ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "retry",
            visible.SuggestedAction ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WeatherResolutionFailureRedactsPathsAndKeepsActionableIdentity()
    {
        const string privatePath = @"C:\private\weather-cache\selected.epw";
        var source = new Diagnostic(
            "SD.WEATHER.EXTRACTION_FAILED",
            DiagnosticSeverity.Fatal,
            "Access to path '" + privatePath + "' was denied.",
            suggestedAction: "Weather cache target: " + privatePath);

        Diagnostic visible = BoundaryDiagnostic(source);

        Assert.Equal(source.Code, visible.Code);
        Assert.Equal(source.Severity, visible.Severity);
        Assert.Equal(
            "The address-selected packaged weather could not be prepared.",
            visible.Message);
        Assert.DoesNotContain(privatePath, visible.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            privatePath,
            visible.SuggestedAction ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "LocalApplicationData",
            visible.SuggestedAction ?? string.Empty,
            StringComparison.Ordinal);
        Assert.Contains(
            "dev install",
            visible.SuggestedAction ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConversionDiagnosticHidesInternalLayerTermsWithoutLosingModelMeaning()
    {
        var source = new Diagnostic(
            "SD.CONVERSION.DOMAIN_INVALID",
            DiagnosticSeverity.Error,
            "The GRM values could not form an InvisibleDragon model: the zone relationship is invalid.",
            new EntityId("ZONE-CONVERSION-BOUNDARY"),
            suggestedAction: "Correct the zone relationship before producing IDF.");

        Diagnostic visible = BoundaryDiagnostic(source);

        Assert.Equal(source.Code, visible.Code);
        Assert.Equal(source.Severity, visible.Severity);
        Assert.Equal(source.ObjectId, visible.ObjectId);
        Assert.Contains("GRM values", visible.Message, StringComparison.Ordinal);
        Assert.Contains("zone relationship", visible.Message, StringComparison.Ordinal);
        Assert.Contains(
            "zone relationship",
            visible.SuggestedAction ?? string.Empty,
            StringComparison.Ordinal);
        Assert.DoesNotContain("InvisibleDragon", visible.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IDF", visible.SuggestedAction ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SavedTrueDoesNotRunAndOnlyRisingEdgesStartOrCancel()
    {
        Type componentType = ComponentType();
        Type gateType = Assert.Single(
            componentType.GetNestedTypes(BindingFlags.NonPublic),
            type => type.Name == "ExplicitRunTriggerGate");
        object gate = Required(Activator.CreateInstance(gateType, nonPublic: true));
        MethodInfo observe = Method(gateType, "Observe", BindingFlags.Instance | BindingFlags.NonPublic);

        AssertObservation(observe.Invoke(gate, new object[] { true, true }), start: false, cancel: false);
        AssertObservation(observe.Invoke(gate, new object[] { false, false }), start: false, cancel: false);
        AssertObservation(observe.Invoke(gate, new object[] { true, false }), start: true, cancel: false);
        AssertObservation(observe.Invoke(gate, new object[] { true, true }), start: false, cancel: true);
        AssertObservation(observe.Invoke(gate, new object[] { true, true }), start: false, cancel: false);
    }

    [Fact]
    public void CacheKeyIsDeterministicAndIncludesTimeoutWhileRunRootStaysInOsTemp()
    {
        Type componentType = ComponentType();
        Type inputsType = Assert.Single(
            componentType.GetNestedTypes(BindingFlags.NonPublic),
            type => type.Name == "RunInputs");
        GreenRetrofitModel model = AddressOnlyModel();
        object thirtyMinutes = Required(Activator.CreateInstance(
            inputsType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { model, TimeSpan.FromMinutes(30) },
            culture: null));
        object sixtyMinutes = Required(Activator.CreateInstance(
            inputsType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { model, TimeSpan.FromMinutes(60) },
            culture: null));
        MethodInfo compute = Method(componentType, "ComputeRunKey", BindingFlags.Static | BindingFlags.NonPublic);

        string first = Assert.IsType<string>(compute.Invoke(null, new[] { thirtyMinutes }));
        string repeated = Assert.IsType<string>(compute.Invoke(null, new[] { thirtyMinutes }));
        string changedTimeout = Assert.IsType<string>(compute.Invoke(null, new[] { sixtyMinutes }));
        Assert.Equal(first, repeated);
        Assert.NotEqual(first, changedTimeout);
        Assert.Matches(new Regex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant), first);

        object automation = CreateAutomationOverrides(
            NestedType(componentType, "AutomationOverrides"),
            runtimeRoot: null,
            iddPath: null,
            weatherPath: null);
        object firstPathInputs = CreateRunInputs(
            inputsType,
            model,
            TimeSpan.FromMinutes(30),
            automation,
            Path.Combine(Path.GetTempPath(), "first.grr"));
        object secondPathInputs = CreateRunInputs(
            inputsType,
            model,
            TimeSpan.FromMinutes(30),
            automation,
            Path.Combine(Path.GetTempPath(), "second.grr"));
        Assert.Equal(
            Assert.IsType<string>(compute.Invoke(null, new[] { firstPathInputs })),
            Assert.IsType<string>(compute.Invoke(null, new[] { secondPathInputs })));

        WeatherMetadata firstMetadata = SimpleDragonDatabase.Default.Weather.Items[0];
        WeatherMetadata secondMetadata = SimpleDragonDatabase.Default.Weather.Items.First(
            item => !string.Equals(
                item.EpwFileName,
                firstMetadata.EpwFileName,
                StringComparison.Ordinal));
        WeatherSelection firstWeather = SimpleDragonDatabase.Default.Weather.FindByAddress(
            firstMetadata.AdministrativeArea,
            new DateTime(2020, 1, 1)).Require();
        WeatherSelection secondWeather = SimpleDragonDatabase.Default.Weather.FindByAddress(
            secondMetadata.AdministrativeArea,
            new DateTime(2020, 1, 1)).Require();
        GreenRetrofitModel firstWeatherModel = AddressOnlyModel(firstWeather);
        GreenRetrofitModel secondWeatherModel = AddressOnlyModel(secondWeather);
        Assert.Equal(
            GrmWriter.Serialize(firstWeatherModel, indented: false),
            GrmWriter.Serialize(secondWeatherModel, indented: false));
        object firstWeatherInputs = CreateRunInputs(inputsType, firstWeatherModel, TimeSpan.FromMinutes(30));
        object secondWeatherInputs = CreateRunInputs(inputsType, secondWeatherModel, TimeSpan.FromMinutes(30));
        Assert.NotEqual(
            Assert.IsType<string>(compute.Invoke(null, new[] { firstWeatherInputs })),
            Assert.IsType<string>(compute.Invoke(null, new[] { secondWeatherInputs })));

        string runRoot = Assert.IsType<string>(
            Method(componentType, "ResolveRunRoot", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null));
        string tempRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        Assert.StartsWith(tempRoot, runRoot, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(
            Path.Combine("Dragons", "simpledragon-runs"),
            runRoot,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComponentReusesOneSimulationExecutorAcrossRuns()
    {
        Type componentType = ComponentType();
        FieldInfo executorField = Assert.IsAssignableFrom<FieldInfo>(componentType.GetField(
            "_simulationExecutor",
            BindingFlags.Instance | BindingFlags.NonPublic));
        GH_Component component = Component();
        object executor = Required(executorField.GetValue(component));

        Assert.True(executorField.IsInitOnly);
        Assert.Equal(typeof(SimpleDragonSimulationExecutor), executorField.FieldType);
        Assert.Same(executor, Required(executorField.GetValue(component)));
        Assert.False(Method(
            componentType,
            "ExecuteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic).IsStatic);
    }

    [Fact]
    public void AutomationOverridesConsumeOnlyTheHostContractAndPreserveManagedDefaults()
    {
        Type componentType = ComponentType();
        Type automationType = NestedType(componentType, "AutomationOverrides");
        MethodInfo capture = Method(automationType, "Capture", BindingFlags.Static | BindingFlags.NonPublic);
        var requestedVariables = new List<string>();
        Func<string, string?> noOverrides = name =>
        {
            requestedVariables.Add(name);
            return null;
        };
        object managed = Required(capture.Invoke(null, new object?[] { noOverrides }));

        Assert.Equal(
            new[]
            {
                "DRAGONS_EXAMPLE_ACTION",
                "DRAGONS_ENERGYPLUS_GATE_STATUS",
            },
            requestedVariables);
        Assert.False(Property<bool>(managed, "HasRuntimeOverride"));
        Assert.Null(PropertyValue(managed, "WeatherPath"));
        EnergyPlusRuntimeResolveOptions managedOptions = Assert.IsType<EnergyPlusRuntimeResolveOptions>(
            Method(automationType, "CreateRuntimeOptions", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(managed, null));
        Assert.Null(managedOptions.RuntimeRoot);
        Assert.False(managedOptions.SearchEnvironmentVariables);
        Assert.True(managedOptions.SearchDefaultCacheLocation);
        Assert.False(managedOptions.SearchDefaultInstallLocation);

        string root = Path.Combine(Path.GetTempPath(), "dragons-example-runtime");
        string idd = Path.Combine(root, "Energy+.idd");
        string weather = Path.Combine(Path.GetTempPath(), "dragons-example-weather.epw");
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DRAGONS_EXAMPLE_ACTION"] = "Validate",
            ["DRAGONS_ENERGYPLUS_GATE_STATUS"] = "ready",
            ["DRAGONS_ENERGYPLUS_ROOT"] = root,
            ["DRAGONS_ENERGYPLUS_IDD"] = idd,
            ["DRAGONS_ENERGYPLUS_WEATHER"] = weather,
        };
        Func<string, string?> readOverrides = name => values.TryGetValue(name, out string? value)
            ? value
            : null;
        object automation = Required(capture.Invoke(null, new object?[] { readOverrides }));
        EnergyPlusRuntimeResolveOptions automationOptions = Assert.IsType<EnergyPlusRuntimeResolveOptions>(
            Method(automationType, "CreateRuntimeOptions", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(automation, null));

        Assert.True(Property<bool>(automation, "HasRuntimeOverride"));
        Assert.Equal(root, automationOptions.RuntimeRoot);
        Assert.False(automationOptions.SearchEnvironmentVariables);
        Assert.False(automationOptions.SearchDefaultCacheLocation);
        Assert.False(automationOptions.SearchDefaultInstallLocation);
        Assert.Equal(weather, Assert.IsType<string>(PropertyValue(automation, "WeatherPath")));
        MethodInfo matchesIdd = Method(
            automationType,
            "MatchesIddPath",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.True(Assert.IsType<bool>(matchesIdd.Invoke(automation, new object[] { idd })));
        Assert.False(Assert.IsType<bool>(matchesIdd.Invoke(
            automation,
            new object[] { Path.Combine(root, "other.idd") })));

        values["DRAGONS_ENERGYPLUS_GATE_STATUS"] = "unavailable";
        object guarded = Required(capture.Invoke(null, new object?[] { readOverrides }));
        Assert.False(Property<bool>(guarded, "HasRuntimeOverride"));
        Assert.Null(PropertyValue(guarded, "WeatherPath"));
    }

    [Fact]
    public void AutomationWeatherOverrideIsValidatedAndUsedWithoutBecomingAnInput()
    {
        Type componentType = ComponentType();
        Type automationType = NestedType(componentType, "AutomationOverrides");
        string weatherPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".epw");
        try
        {
            File.WriteAllText(weatherPath, "LOCATION,Seoul,KR\nDATA,fixture");
            object automation = CreateAutomationOverrides(
                automationType,
                runtimeRoot: null,
                iddPath: null,
                weatherPath);
            WeatherSelection selection = SimpleDragonDatabase.Default.Weather.FindByAddress(
                SimpleDragonDatabase.Default.Weather.Items[0].AdministrativeArea,
                new DateTime(2020, 1, 1)).Require();
            SimpleDragonWeatherFileResolution resolution =
                Assert.IsType<SimpleDragonWeatherFileResolution>(Method(
                        componentType,
                        "ResolveWeather",
                        BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { selection, automation, CancellationToken.None }));

            Assert.True(resolution.IsSuccess);
            Assert.Equal(Path.GetFullPath(weatherPath), resolution.FilePath);
            Assert.Null(resolution.ArchivePath);
            Assert.False(resolution.Extracted);
            Assert.Empty(resolution.Diagnostics);
            Assert.Equal(
                new[] { "GRM", "Run", "Cancel", "Force Rerun", "Timeout", "GRR Path" },
                Component().Params.Input.Select(parameter => parameter.Name));

            string firstIdentity = Property<string>(automation, "CacheIdentity");
            File.WriteAllText(weatherPath, "LOCATION,Busan,KR\nDATA,changed-in-place");
            SimpleDragonWeatherFileResolution changedInPlace =
                Assert.IsType<SimpleDragonWeatherFileResolution>(Method(
                        componentType,
                        "ResolveWeather",
                        BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { selection, automation, CancellationToken.None }));
            Diagnostic diagnostic = Assert.Single(changedInPlace.Diagnostics);
            Assert.False(changedInPlace.IsSuccess);
            Assert.Null(changedInPlace.FilePath);
            Assert.Equal("SD.GH.RUN_AUTOMATION_WEATHER_INVALID", diagnostic.Code);
            Assert.DoesNotContain(weatherPath, diagnostic.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                weatherPath,
                diagnostic.SuggestedAction ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);

            object refreshed = CreateAutomationOverrides(
                automationType,
                runtimeRoot: null,
                iddPath: null,
                weatherPath);
            Assert.NotEqual(firstIdentity, Property<string>(refreshed, "CacheIdentity"));

            string missingPath = weatherPath + ".missing.epw";
            object missing = CreateAutomationOverrides(
                automationType,
                runtimeRoot: null,
                iddPath: null,
                missingPath);
            SimpleDragonWeatherFileResolution missingResolution =
                Assert.IsType<SimpleDragonWeatherFileResolution>(Method(
                        componentType,
                        "ResolveWeather",
                        BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { selection, missing, CancellationToken.None }));
            Diagnostic missingDiagnostic = Assert.Single(missingResolution.Diagnostics);
            Assert.Equal("SD.GH.RUN_AUTOMATION_WEATHER_INVALID", missingDiagnostic.Code);
            Assert.DoesNotContain(missingPath, missingDiagnostic.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                missingPath,
                missingDiagnostic.SuggestedAction ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(weatherPath);
        }
    }

    [Fact]
    public async Task ExplicitAutomationRuntimeFailureNeverFallsBackToManagedBootstrap()
    {
        Type componentType = ComponentType();
        Type automationType = NestedType(componentType, "AutomationOverrides");
        string missingRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        object automation = CreateAutomationOverrides(
            automationType,
            missingRoot,
            Path.Combine(missingRoot, "Energy+.idd"),
            weatherPath: null);
        var states = new List<string>();
        object taskValue = Required(Method(
                componentType,
                "ResolveRuntimeAsync",
                BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[]
            {
                automation,
                (Action<string>)states.Add,
                CancellationToken.None,
            }));
        Task task = Assert.IsAssignableFrom<Task>(taskValue);

        await task;

        object preparation = Required(PropertyValue(taskValue, "Result"));
        Assert.False(Property<bool>(preparation, "IsSuccess"));
        EnergyPlusFailure failure = Property<EnergyPlusFailure>(preparation, "Failure");
        Assert.Equal(EnergyPlusFailureCategory.RuntimeNotFound, failure.Category);
        Assert.Equal("RUNTIME_NOT_FOUND", failure.Code);
        Assert.DoesNotContain(states, state => state.StartsWith("Preparing Runtime", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimeFailureDiagnosticPreservesStructureWithoutLeakingPathsOrInvisibleDragon()
    {
        Type componentType = ComponentType();
        MethodInfo create = Method(
            componentType,
            "CreateRuntimeFailureDiagnostic",
            BindingFlags.Static | BindingFlags.NonPublic);
        const string privatePath = @"C:\private\runtime\EnergyPlus.exe";
        var failure = new EnergyPlusFailure(
            EnergyPlusFailureCategory.RuntimeEnvironment,
            "RUNTIME_BOOTSTRAP_IO_FAILED",
            "The EnergyPlus runtime cache could not be prepared.",
            privatePath);

        Diagnostic managed = Assert.IsType<Diagnostic>(create.Invoke(
            null,
            new object[] { failure, false }));
        Assert.Equal(
            "SD.GH.RUN.RUNTIME.RUNTIMEENVIRONMENT.RUNTIME_BOOTSTRAP_IO_FAILED",
            managed.Code);
        Assert.Equal(failure.Message, managed.Message);
        Assert.Contains("LocalApplicationData", managed.SuggestedAction, StringComparison.Ordinal);
        Assert.Contains("dev install", managed.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(privatePath, managed.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(privatePath, managed.SuggestedAction ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvisibleDragon", managed.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "InvisibleDragon",
            managed.SuggestedAction ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        Diagnostic automation = Assert.IsType<Diagnostic>(create.Invoke(
            null,
            new object[] { failure, true }));
        Assert.Equal(
            "SD.GH.RUN.AUTOMATION_RUNTIME.RUNTIMEENVIRONMENT.RUNTIME_BOOTSTRAP_IO_FAILED",
            automation.Code);
        Assert.Contains("Rhino example gate", automation.SuggestedAction, StringComparison.Ordinal);
        Assert.DoesNotContain("LocalApplicationData", automation.SuggestedAction, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomationOverrideIdentityParticipatesInTheSuccessCacheKey()
    {
        Type componentType = ComponentType();
        Type inputsType = NestedType(componentType, "RunInputs");
        Type automationType = NestedType(componentType, "AutomationOverrides");
        GreenRetrofitModel model = AddressOnlyModel();
        object firstAutomation = CreateAutomationOverrides(
            automationType,
            @"C:\runtime-a",
            @"C:\runtime-a\Energy+.idd",
            @"C:\weather-a.epw");
        object secondAutomation = CreateAutomationOverrides(
            automationType,
            @"C:\runtime-b",
            @"C:\runtime-b\Energy+.idd",
            @"C:\weather-b.epw");
        object firstInputs = CreateRunInputs(
            inputsType,
            model,
            TimeSpan.FromMinutes(30),
            firstAutomation);
        object secondInputs = CreateRunInputs(
            inputsType,
            model,
            TimeSpan.FromMinutes(30),
            secondAutomation);
        MethodInfo compute = Method(componentType, "ComputeRunKey", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotEqual(
            Assert.IsType<string>(compute.Invoke(null, new[] { firstInputs })),
            Assert.IsType<string>(compute.Invoke(null, new[] { secondInputs })));
    }

    [Fact]
    public void OnlySuccessfulMatchingResultsAreCacheableAndForceAlwaysExecutes()
    {
        Type componentType = ComponentType();
        Type outcomeType = Assert.Single(
            componentType.GetNestedTypes(BindingFlags.NonPublic),
            type => type.Name == "RunOutcome");
        GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(
            1d,
            EnergyUseBreakdown.Create((_, _) => Enumerable.Repeat(0d, MonthlySeries.MonthCount)));
        object successful = CreateOutcome(outcomeType, "Succeeded", success: true, result);
        object failed = CreateOutcome(outcomeType, "Failed", success: false, result: null);
        MethodInfo canReuse = Method(
            componentType,
            "CanReuseLastOutcome",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.True(Assert.IsType<bool>(canReuse.Invoke(
            null,
            new object?[] { false, successful, "same", "same" })));
        Assert.False(Assert.IsType<bool>(canReuse.Invoke(
            null,
            new object?[] { true, successful, "same", "same" })));
        Assert.False(Assert.IsType<bool>(canReuse.Invoke(
            null,
            new object?[] { false, successful, "old", "new" })));
        Assert.False(Assert.IsType<bool>(canReuse.Invoke(
            null,
            new object?[] { false, failed, "same", "same" })));
        Assert.False(Assert.IsType<bool>(canReuse.Invoke(
            null,
            new object?[] { false, null, "same", "same" })));
    }

    [Fact]
    public void ActiveOrMismatchedOutcomesStayHiddenWithExplicitWarnings()
    {
        Type componentType = ComponentType();
        Type outcomeType = NestedType(componentType, "RunOutcome");
        Type visibilityType = NestedType(componentType, "OutcomeVisibility");
        GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(
            1d,
            EnergyUseBreakdown.Create((_, _) => Enumerable.Repeat(0d, MonthlySeries.MonthCount)));
        object successful = CreateOutcome(outcomeType, "Succeeded", success: true, result);
        MethodInfo classify = Method(
            componentType,
            "ClassifyOutcome",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo warning = Method(
            componentType,
            "HiddenOutcomeWarning",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo visibleState = Method(
            componentType,
            "VisibleState",
            BindingFlags.Static | BindingFlags.NonPublic);

        object current = Required(classify.Invoke(
            null,
            new object?[] { false, successful, "same", "same" }));
        Assert.Equal("Current", current.ToString());
        Assert.Null(warning.Invoke(null, new[] { current }));
        Assert.Equal(
            "Succeeded",
            Assert.IsType<string>(visibleState.Invoke(
                null,
                new object[] { "Succeeded", current })));

        object forcedSameKeyRun = Required(classify.Invoke(
            null,
            new object?[] { true, successful, "same", "same" }));
        Assert.Equal("HiddenWhileRunning", forcedSameKeyRun.ToString());
        Assert.Contains(
            "hidden while a SimpleDragon run is active",
            Assert.IsType<string>(warning.Invoke(null, new[] { forcedSameKeyRun })),
            StringComparison.Ordinal);
        Assert.Equal(
            "Running Simulation",
            Assert.IsType<string>(visibleState.Invoke(
                null,
                new object[] { "Running Simulation", forcedSameKeyRun })));

        object changedInputs = Required(classify.Invoke(
            null,
            new object?[] { false, successful, "old", "new" }));
        Assert.Equal("HiddenForDifferentInputs", changedInputs.ToString());
        Assert.Contains(
            "belongs to different inputs",
            Assert.IsType<string>(warning.Invoke(null, new[] { changedInputs })),
            StringComparison.Ordinal);
        Assert.Equal(
            "Inputs Changed",
            Assert.IsType<string>(visibleState.Invoke(
                null,
                new object[] { "Succeeded", changedInputs })));

        object noOutcome = Required(classify.Invoke(
            null,
            new object?[] { true, null, null, "new" }));
        Assert.Equal("None", noOutcome.ToString());
        Assert.Null(warning.Invoke(null, new[] { noOutcome }));
        Assert.Equal(
            "Idle",
            Assert.IsType<string>(visibleState.Invoke(
                null,
                new object[] { "Idle", noOutcome })));
        Assert.True(visibilityType.IsEnum);
    }

    [Fact]
    public void SolutionScheduleGateCoalescesUntilTheScheduledCallbackReleasesIt()
    {
        Type gateType = NestedType(ComponentType(), "SolutionScheduleGate");
        object gate = Required(Activator.CreateInstance(gateType, nonPublic: true));
        MethodInfo request = Method(gateType, "TryRequest", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo release = Method(gateType, "Release", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.True(Assert.IsType<bool>(request.Invoke(gate, null)));
        Assert.False(Assert.IsType<bool>(request.Invoke(gate, null)));
        Assert.False(Assert.IsType<bool>(request.Invoke(gate, null)));

        release.Invoke(gate, null);

        Assert.True(Assert.IsType<bool>(request.Invoke(gate, null)));
        Assert.False(Assert.IsType<bool>(request.Invoke(gate, null)));
    }

    [Fact]
    public void CancellationAndInternalFailuresRemainPathFreeSimpleDragonDiagnostics()
    {
        Type outcomeType = Assert.Single(
            ComponentType().GetNestedTypes(BindingFlags.NonPublic),
            type => type.Name == "RunOutcome");
        object cancelled = Required(
            Method(outcomeType, "Cancelled", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null));
        Assert.Equal("Cancelled", Property<string>(cancelled, "State"));
        Diagnostic cancellation = Assert.Single(Property<IReadOnlyList<Diagnostic>>(cancelled, "Diagnostics"));
        Assert.Equal("SD.GH.RUN_CANCELLED", cancellation.Code);
        Assert.False(cancellation.IsFailure);

        const string privatePath = @"C:\private\runtime\EnergyPlus.exe";
        object failed = Required(
            Method(outcomeType, "InternalFailure", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { new IOException(privatePath) }));
        Assert.Equal("Failed", Property<string>(failed, "State"));
        Diagnostic failure = Assert.Single(Property<IReadOnlyList<Diagnostic>>(failed, "Diagnostics"));
        Assert.Equal("SD.GH.RUN_INTERNAL_ERROR", failure.Code);
        Assert.DoesNotContain(privatePath, failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(failure.SuggestedAction);
    }

    private static GreenRetrofitModel AddressOnlyModel(WeatherSelection? weather = null)
    {
        WeatherMetadata metadata = SimpleDragonDatabase.Default.Weather.Items[0];
        return new GreenRetrofitModel(
            "Direct Run Contract Model",
            0,
            metadata.AdministrativeArea,
            new DateTime(2020, 1, 1),
            false,
            Array.Empty<BuildingFloor>(),
            Array.Empty<Material>(),
            Array.Empty<SurfaceConstruction>(),
            Array.Empty<FenestrationConstruction>(),
            weather: weather);
    }

    private static object CreateRunInputs(Type inputsType, GreenRetrofitModel model, TimeSpan timeout) =>
        Required(Activator.CreateInstance(
            inputsType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { model, timeout },
            culture: null));

    private static object CreateRunInputs(
        Type inputsType,
        GreenRetrofitModel model,
        TimeSpan timeout,
        object automation) =>
        Required(Activator.CreateInstance(
            inputsType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new[] { (object)model, timeout, automation },
            culture: null));

    private static object CreateRunInputs(
        Type inputsType,
        GreenRetrofitModel model,
        TimeSpan timeout,
        object automation,
        string? resultPath) =>
        Required(Activator.CreateInstance(
            inputsType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object?[] { model, timeout, automation, resultPath },
            culture: null));

    private static object CreateAutomationOverrides(
        Type automationType,
        string? runtimeRoot,
        string? iddPath,
        string? weatherPath) =>
        Required(Activator.CreateInstance(
            automationType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object?[] { runtimeRoot, iddPath, weatherPath },
            culture: null));

    private static object CreateOutcome(
        Type outcomeType,
        string state,
        bool success,
        GreenRetrofitResult? result) =>
        Required(Activator.CreateInstance(
            outcomeType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object?[] { state, success, result, Array.Empty<Diagnostic>() },
            culture: null));

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
        IEnumerable? data = allData?.Invoke(structure, new object[] { true }) as IEnumerable;
        object? first = data?.Cast<object>().FirstOrDefault();
        return first is IGH_Goo goo ? goo.ScriptVariable() : first;
    }

    private static void AssertObservation(object? observation, bool start, bool cancel)
    {
        object value = Required(observation);
        Assert.Equal(start, Property<bool>(value, "Start"));
        Assert.Equal(cancel, Property<bool>(value, "Cancel"));
    }

    private static Diagnostic BoundaryDiagnostic(Diagnostic diagnostic)
    {
        Type outcomeType = NestedType(ComponentType(), "RunOutcome");
        object outcome = Required(
            Method(outcomeType, "Failed", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { new[] { diagnostic } }));
        return Assert.Single(Property<IReadOnlyList<Diagnostic>>(outcome, "Diagnostics"));
    }

    private static T Property<T>(object value, string name)
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(value.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        return Assert.IsAssignableFrom<T>(property.GetValue(value));
    }

    private static object? PropertyValue(object value, string name) =>
        Assert.IsAssignableFrom<PropertyInfo>(value.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .GetValue(value);

    private static Type NestedType(Type type, string name) => Assert.Single(
        type.GetNestedTypes(BindingFlags.NonPublic),
        candidate => candidate.Name == name);

    private static MethodInfo Method(Type type, string name, BindingFlags flags) =>
        Assert.IsAssignableFrom<MethodInfo>(type.GetMethod(name, flags));

    private static GH_Component Component() =>
        Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(ComponentType()));

    private static Type ComponentType() => Assert.IsAssignableFrom<Type>(LoadPlugin().GetType(ComponentTypeName));

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
