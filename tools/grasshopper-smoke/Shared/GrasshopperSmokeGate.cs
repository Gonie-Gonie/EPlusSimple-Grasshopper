using System.Reflection;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace GonieGonie.Dragons.GrasshopperSmoke;

internal static class GrasshopperSmokeGate
{
    private const string HostAssemblyPrefix = "GonieGonie.Dragons.Grasshopper";
    private static readonly string[] KnownPluginAssemblies =
    {
        "GonieGonie.InvisibleDragon.GH",
        "GonieGonie.SimpleDragon.GH"
    };

    internal static void BlockInstalledDragonPackages()
    {
        const string fieldName = "_package_folder_blocklist";
        FieldInfo? field = typeof(Rhino.Runtime.HostUtils).GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic);
        if (field is null)
        {
            Require(
                typeof(Rhino.Runtime.HostUtils).Assembly.GetName().Version?.Major < 8,
                "Rhino 8 no longer exposes the package-folder isolation blocklist.");
            return;
        }
        Require(
            field.FieldType == typeof(string[]),
            "Rhino's package-folder blocklist has an unexpected type.");

        string[] existing = (string[]?)field.GetValue(null) ?? Array.Empty<string>();
        string[] required =
        {
            "invisible-dragon",
            "simple-dragon"
        };
        string[] blocked = existing
            .Concat(required)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        field.SetValue(null, blocked);

        string[] applied = (string[]?)field.GetValue(null) ?? Array.Empty<string>();
        Require(
            required.All(package => applied.Contains(package, StringComparer.OrdinalIgnoreCase)),
            "Rhino did not apply the Dragon package-folder isolation blocklist.");
    }

    internal static void RestrictExternalLibraries(IReadOnlyList<string> pluginPaths)
    {
        MethodInfo method = typeof(GH_ComponentServer).GetMethod(
            "SetExternalGHAs",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(IEnumerable<string>) },
            modifiers: null)
            ?? throw new MissingMethodException(typeof(GH_ComponentServer).FullName, "SetExternalGHAs");
        method.Invoke(null, new object[] { pluginPaths });
    }

    internal static GrasshopperSmokeSummary Run(
        SmokeHostInputs inputs,
        string host,
        string rhinoVersion)
    {
        GH_ComponentServer server = Instances.ComponentServer;
        IReadOnlyList<DragonModuleSpec> moduleSpecs = DragonModuleSpec.FromInputs(inputs);
        var catalogs = moduleSpecs.Select(DiscoverModule).ToArray();
        ExpectedObject[] allExpected = catalogs.SelectMany(catalog => catalog.All).ToArray();
        Require(allExpected.Length > 0, "No Dragon Grasshopper objects were discovered.");
        Require(
            allExpected.Select(item => item.Id).Distinct().Count() == allExpected.Length,
            "Dragon component and parameter GUIDs are not unique across the requested scenario.");

        Progress("loading only the requested Dragon Grasshopper libraries");
        foreach (ModuleCatalog catalog in catalogs)
        {
            EnsureRegistered(server, catalog.Components, catalog.PluginAssembly, catalog.Spec.PluginPath);
            EnsureRegistered(server, catalog.Parameters, catalog.TypesAssembly, catalog.Spec.TypesPath);
        }

        AssertAbsentModuleProxies(server, catalogs);
        AssertExpectedPluginAssemblySet(catalogs);

        Progress("creating a document containing every discovered public Dragon component and parameter");
        var document = new GH_Document();
        var persistenceTargets = new List<PersistenceTarget>();
        foreach (ModuleCatalog catalog in catalogs)
        {
            foreach (ExpectedObject component in catalog.Components)
            {
                document.AddObject(Emit(server, component), update: false, index: document.ObjectCount);
            }

            foreach (ExpectedObject parameter in catalog.Parameters)
            {
                IGH_DocumentObject instance = Emit(server, parameter);
                if (string.Equals(
                        parameter.RuntimeType.FullName,
                        catalog.Spec.PersistenceParameterType,
                        StringComparison.Ordinal))
                {
                    AddPersistentGoo(instance, catalog);
                    persistenceTargets.Add(new PersistenceTarget(
                        catalog,
                        instance.InstanceGuid));
                }

                document.AddObject(instance, update: false, index: document.ObjectCount);
            }
        }

        Require(
            persistenceTargets.Count == catalogs.Length,
            "Each requested Dragon module must expose exactly one representative persistence target.");

        Directory.CreateDirectory(Path.GetDirectoryName(inputs.DocumentPath)!);
        var writer = new GH_DocumentIO(document);
        Progress("saving the complete proxy and persistence document");
        Require(writer.SaveQuiet(inputs.DocumentPath), "Grasshopper document save failed.");
        Require(File.Exists(inputs.DocumentPath), "Grasshopper reported success but did not create the document.");

        var reader = new GH_DocumentIO();
        Progress("reopening the complete proxy and persistence document");
        Require(reader.Open(inputs.DocumentPath), "Grasshopper document reopen failed.");
        GH_Document reopened = reader.Document
            ?? throw new InvalidOperationException("Grasshopper reopened the file without a document.");
        Require(
            reopened.ObjectCount == document.ObjectCount,
            $"Expected {document.ObjectCount} reopened objects; got {reopened.ObjectCount}.");

        foreach (ExpectedObject expected in allExpected)
        {
            Require(
                reopened.Objects.Any(item => item.ComponentGuid == expected.Id),
                $"Reopened document lost {expected.RuntimeType.FullName} ({expected.Id}).");
        }

        var persistence = new List<PersistenceSummary>();
        foreach (PersistenceTarget target in persistenceTargets)
        {
            IGH_DocumentObject reopenedParameter = reopened.FindObject(
                    target.InstanceGuid,
                    topLevelOnly: true)
                ?? throw new InvalidOperationException(
                    "Reopened document lost the " + target.Catalog.Spec.Product + " persistence parameter.");
            persistence.Add(ReadPersistentGoo(reopenedParameter, target.Catalog));
        }

        AssertAbsentModuleProxies(server, catalogs);
        AssertLoadedAssemblyOrigins(inputs);
        foreach (SmokeArtifactProvenance artifact in inputs.PluginArtifacts.Concat(inputs.PortableArchives))
        {
            artifact.VerifyUnchanged();
        }

        var summary = new GrasshopperSmokeSummary
        {
            Host = host,
            RhinoVersion = rhinoVersion,
            GrasshopperVersion = typeof(Instances).Assembly.GetName().Version?.ToString() ?? "unknown",
            Scenario = inputs.Scenario.ToString(),
            Source = inputs.Source,
            PluginCount = inputs.PluginPaths.Count,
            PluginPaths = inputs.PluginPaths.ToArray(),
            PluginArtifacts = inputs.PluginArtifacts
                .Select(ArtifactProvenanceSummary.From)
                .ToArray(),
            PortableArchives = inputs.PortableArchives
                .Select(ArtifactProvenanceSummary.From)
                .ToArray(),
            RegisteredInvisibleComponents = Count(catalogs, "InvisibleDragon", components: true),
            RegisteredInvisibleParameters = Count(catalogs, "InvisibleDragon", components: false),
            RegisteredSimpleComponents = Count(catalogs, "SimpleDragon", components: true),
            RegisteredSimpleParameters = Count(catalogs, "SimpleDragon", components: false),
            ReopenedObjectCount = reopened.ObjectCount,
            Persistence = persistence.ToArray(),
            DocumentPath = inputs.DocumentPath
        };
        summary.Write(inputs.SummaryPath);
        summary.WriteLegacyText(inputs.DocumentPath + ".summary.txt");
        return summary;
    }

    private static ModuleCatalog DiscoverModule(DragonModuleSpec spec)
    {
        Require(File.Exists(spec.PluginPath), "Dragon GHA is absent: " + spec.PluginPath);
        Require(File.Exists(spec.TypesPath), "Dragon Types assembly is absent: " + spec.TypesPath);
        Assembly pluginAssembly = LoadExact(spec.PluginPath, spec.PluginAssemblyName);
        Assembly typesAssembly = LoadExact(spec.TypesPath, spec.TypesAssemblyName);
        ExpectedObject[] components = Discover(
            pluginAssembly,
            type => typeof(GH_Component).IsAssignableFrom(type));
        ExpectedObject[] parameters = Discover(
            typesAssembly,
            type => typeof(IGH_Param).IsAssignableFrom(type)
                && type.Namespace is not null
                && type.Namespace.StartsWith(spec.ParameterNamespace, StringComparison.Ordinal));
        Require(components.Length > 0, "No public " + spec.Product + " components were discovered.");
        Require(parameters.Length > 0, "No public " + spec.Product + " parameters were discovered.");
        Require(
            components.Concat(parameters).Select(item => item.Id).Distinct().Count()
                == components.Length + parameters.Length,
            spec.Product + " component and parameter GUIDs are not unique.");
        return new ModuleCatalog(spec, pluginAssembly, typesAssembly, components, parameters);
    }

    private static ExpectedObject[] Discover(Assembly assembly, Func<Type, bool> predicate)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            string details = string.Join(
                Environment.NewLine,
                exception.LoaderExceptions
                    .Where(loader => loader is not null)
                    .Select(loader => loader!.ToString()));
            throw new InvalidOperationException(
                "Could not reflect all public types from '" + assembly.Location + "'."
                    + Environment.NewLine + details,
                exception);
        }

        return types
            .Where(type => type.IsPublic
                && !type.IsAbstract
                && !type.ContainsGenericParameters
                && predicate(type))
            .Select(type =>
            {
                var instance = Activator.CreateInstance(type) as IGH_DocumentObject
                    ?? throw new InvalidOperationException(
                        "Could not construct discovered Grasshopper object '" + type.FullName + "'.");
                Require(
                    instance.ComponentGuid != Guid.Empty,
                    "Discovered Grasshopper object has an empty GUID: " + type.FullName);
                return new ExpectedObject(type, instance.ComponentGuid);
            })
            .OrderBy(item => item.RuntimeType.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static void EnsureRegistered(
        GH_ComponentServer server,
        IReadOnlyList<ExpectedObject> expectedObjects,
        Assembly assembly,
        string path)
    {
        foreach (ExpectedObject expected in expectedObjects)
        {
            IGH_ObjectProxy? existing = server.EmitObjectProxy(expected.Id);
            if (existing is not null)
            {
                AssertProxy(existing, expected, path);
            }
        }

        if (expectedObjects.Any(expected => server.EmitObjectProxy(expected.Id) is null))
        {
            ParseExternalLibrary(server, assembly, path);
        }

        foreach (ExpectedObject expected in expectedObjects)
        {
            IGH_ObjectProxy proxy = server.EmitObjectProxy(expected.Id)
                ?? throw new InvalidOperationException(
                    $"Grasshopper did not register {expected.RuntimeType.FullName} ({expected.Id}).");
            AssertProxy(proxy, expected, path);
            _ = Emit(server, expected);
        }
    }

    private static void AssertProxy(IGH_ObjectProxy proxy, ExpectedObject expected, string expectedPath)
    {
        Type proxyType = proxy.Type
            ?? throw new InvalidOperationException("A compiled Dragon proxy has no runtime Type.");
        Require(
            string.Equals(proxyType.FullName, expected.RuntimeType.FullName, StringComparison.Ordinal),
            $"Proxy {expected.Id} resolves to {proxyType.FullName} instead of {expected.RuntimeType.FullName}.");
        Require(
            PathsEqual(proxyType.Assembly.Location, expectedPath),
            $"Proxy {expected.RuntimeType.FullName} came from '{proxyType.Assembly.Location}' "
                + $"instead of the requested payload '{expectedPath}'.");
    }

    private static void ParseExternalLibrary(GH_ComponentServer server, Assembly assembly, string path)
    {
        MethodInfo parser = server.GetType().GetMethod(
            "ParseGHA",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(Assembly), typeof(string) },
            modifiers: null)
            ?? throw new MissingMethodException(server.GetType().FullName, "ParseGHA(Assembly, string)");
        parser.Invoke(server, new object[] { assembly, path });
    }

    private static IGH_DocumentObject Emit(GH_ComponentServer server, ExpectedObject expected)
    {
        IGH_DocumentObject emitted = server.EmitObject(expected.Id)
            ?? throw new InvalidOperationException(
                $"Grasshopper could not instantiate {expected.RuntimeType.FullName} ({expected.Id}).");
        Require(
            string.Equals(emitted.GetType().FullName, expected.RuntimeType.FullName, StringComparison.Ordinal),
            $"Grasshopper emitted {emitted.GetType().FullName} instead of {expected.RuntimeType.FullName}.");
        return emitted;
    }

    private static void AddPersistentGoo(IGH_DocumentObject parameter, ModuleCatalog catalog)
    {
        object goo = string.Equals(catalog.Spec.Product, "InvisibleDragon", StringComparison.Ordinal)
            ? CreateInvisibleDiagnosticGoo(catalog)
            : CreateSimpleMaterialGoo(catalog);
        MethodInfo add = parameter.GetType().GetMethod(
            "AddPersistentData",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(object) },
            modifiers: null)
            ?? throw new MissingMethodException(parameter.GetType().FullName, "AddPersistentData(object)");
        bool added = add.Invoke(parameter, new[] { goo }) as bool? ?? false;
        Require(added, catalog.Spec.Product + " parameter rejected its representative Goo value.");
    }

    private static object CreateInvisibleDiagnosticGoo(ModuleCatalog catalog)
    {
        Type gooType = catalog.TypesAssembly.GetType(
            catalog.Spec.PersistenceGooType,
            throwOnError: true)!;
        ConstructorInfo gooConstructor = RequireValueConstructor(gooType);
        Type diagnosticType = gooConstructor.GetParameters()[0].ParameterType;
        Type severityType = diagnosticType.Assembly.GetType(
            "GonieGonie.BuildingEnergy.Contracts.DiagnosticSeverity",
            throwOnError: true)!;
        object severity = Enum.Parse(severityType, "Info", ignoreCase: false);
        ConstructorInfo diagnosticConstructor = diagnosticType.GetConstructors()
            .Single(constructor => constructor.GetParameters().Length == 6);
        object diagnostic = diagnosticConstructor.Invoke(new object?[]
        {
            catalog.Spec.PersistenceValue,
            severity,
            "Grasshopper portable-package host persistence gate.",
            null,
            null,
            "Retain this value after save and reopen."
        });
        return gooConstructor.Invoke(new[] { diagnostic });
    }

    private static object CreateSimpleMaterialGoo(ModuleCatalog catalog)
    {
        Type gooType = catalog.TypesAssembly.GetType(
            catalog.Spec.PersistenceGooType,
            throwOnError: true)!;
        ConstructorInfo gooConstructor = RequireValueConstructor(gooType);
        Type materialType = gooConstructor.GetParameters()[0].ParameterType;
        object material = Activator.CreateInstance(
            materialType,
            new object?[] { catalog.Spec.PersistenceValue, 0.04, 30.0, 1400.0, null })
            ?? throw new InvalidOperationException("Could not construct the SimpleDragon material value.");
        return gooConstructor.Invoke(new[] { material });
    }

    private static ConstructorInfo RequireValueConstructor(Type gooType)
    {
        return gooType.GetConstructors().Single(constructor => constructor.GetParameters().Length == 1);
    }

    private static PersistenceSummary ReadPersistentGoo(
        IGH_DocumentObject parameter,
        ModuleCatalog catalog)
    {
        PropertyInfo countProperty = parameter.GetType().GetProperty("PersistentDataCount")
            ?? throw new MissingMemberException(parameter.GetType().FullName, "PersistentDataCount");
        int count = countProperty.GetValue(parameter) as int? ?? 0;
        Require(count == 1, $"Expected one persistent Goo value; got {count}.");

        PropertyInfo dataProperty = parameter.GetType().GetProperty("PersistentData")
            ?? throw new MissingMemberException(parameter.GetType().FullName, "PersistentData");
        object tree = dataProperty.GetValue(parameter)
            ?? throw new InvalidOperationException("Reopened parameter has no persistent data tree.");
        MethodInfo allData = tree.GetType().GetMethod("AllData", new[] { typeof(bool) })
            ?? throw new MissingMethodException(tree.GetType().FullName, "AllData(bool)");
        var values = (System.Collections.IEnumerable)allData.Invoke(tree, new object[] { false })!;
        object gooObject = values.Cast<object>().Single();
        Require(
            string.Equals(
                gooObject.GetType().FullName,
                catalog.Spec.PersistenceGooType,
                StringComparison.Ordinal),
            $"Reopened Goo is {gooObject.GetType().FullName} instead of {catalog.Spec.PersistenceGooType}.");
        Require(gooObject is IGH_Goo, "Reopened persistent value does not implement IGH_Goo.");
        object domain = ((IGH_Goo)gooObject).ScriptVariable()
            ?? throw new InvalidOperationException("Reopened Goo has no domain value.");
        string value = domain.GetType().GetProperty(catalog.Spec.PersistenceProperty)?.GetValue(domain) as string
            ?? throw new MissingMemberException(domain.GetType().FullName, catalog.Spec.PersistenceProperty);
        Require(
            string.Equals(value, catalog.Spec.PersistenceValue, StringComparison.Ordinal),
            catalog.Spec.Product + " Goo domain value changed during save and reopen.");
        return new PersistenceSummary
        {
            Product = catalog.Spec.Product,
            GooType = gooObject.GetType().FullName ?? gooObject.GetType().Name,
            ValueProperty = catalog.Spec.PersistenceProperty,
            Value = value
        };
    }

    private static void AssertAbsentModuleProxies(
        GH_ComponentServer server,
        IReadOnlyList<ModuleCatalog> catalogs)
    {
        var expectedAssemblies = new HashSet<string>(
            catalogs.Select(catalog => catalog.Spec.PluginAssemblyName),
            StringComparer.Ordinal);
        foreach (string absentAssembly in KnownPluginAssemblies.Where(name => !expectedAssemblies.Contains(name)))
        {
            IGH_ObjectProxy? proxy = server.ObjectProxies.FirstOrDefault(item =>
                string.Equals(item.Type?.Assembly.GetName().Name, absentAssembly, StringComparison.Ordinal)
                    || string.Equals(
                        Path.GetFileNameWithoutExtension(item.Location),
                        absentAssembly,
                        StringComparison.OrdinalIgnoreCase));
            Require(
                proxy is null,
                $"Scenario unexpectedly registered an absent-module proxy from {absentAssembly}: "
                    + $"{proxy?.Type?.FullName} ({proxy?.Guid}).");
        }
    }

    private static void AssertExpectedPluginAssemblySet(IReadOnlyList<ModuleCatalog> catalogs)
    {
        var expected = new HashSet<string>(
            catalogs.Select(catalog => catalog.Spec.PluginAssemblyName),
            StringComparer.Ordinal);
        var loaded = new HashSet<string>(
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetName().Name)
                .Where(name => name is not null && KnownPluginAssemblies.Contains(name, StringComparer.Ordinal))!
                .Cast<string>(),
            StringComparer.Ordinal);
        Require(
            expected.SetEquals(loaded),
            "Loaded Dragon GHA assembly set does not match the requested scenario. Expected "
                + string.Join(", ", expected) + "; loaded " + string.Join(", ", loaded) + ".");
    }

    private static void AssertLoadedAssemblyOrigins(SmokeHostInputs inputs)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string? name = assembly.GetName().Name;
            if (string.IsNullOrWhiteSpace(name)
                || !name.StartsWith("GonieGonie.", StringComparison.Ordinal)
                || name.StartsWith(HostAssemblyPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string location = assembly.Location;
            Require(
                !string.IsNullOrWhiteSpace(location)
                    && inputs.AllowedPluginRoots.Any(root => SmokeHostInputs.IsWithin(root, location)),
                $"Dragon dependency '{name}' resolved outside the permitted payload roots: '{location}'.");
        }
    }

    private static Assembly LoadExact(string path, string expectedAssemblyName)
    {
        Assembly assembly = Assembly.LoadFrom(path);
        Require(
            string.Equals(assembly.GetName().Name, expectedAssemblyName, StringComparison.Ordinal),
            $"Expected assembly {expectedAssemblyName}; loaded {assembly.GetName().Name} from {path}.");
        Require(
            PathsEqual(assembly.Location, path),
            $"Assembly {expectedAssemblyName} resolved from '{assembly.Location}' instead of '{path}'.");
        return assembly;
    }

    private static int Count(
        IReadOnlyList<ModuleCatalog> catalogs,
        string product,
        bool components)
    {
        ModuleCatalog? catalog = catalogs.SingleOrDefault(item =>
            string.Equals(item.Spec.Product, product, StringComparison.Ordinal));
        if (catalog is null)
        {
            return 0;
        }

        return components ? catalog.Components.Count : catalog.Parameters.Count;
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void Progress(string message)
    {
        Console.WriteLine("[grasshopper-smoke] " + message);
        Console.Out.Flush();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ExpectedObject
    {
        internal ExpectedObject(Type runtimeType, Guid id)
        {
            RuntimeType = runtimeType;
            Id = id;
        }

        internal Type RuntimeType { get; }

        internal Guid Id { get; }
    }

    private sealed class ModuleCatalog
    {
        internal ModuleCatalog(
            DragonModuleSpec spec,
            Assembly pluginAssembly,
            Assembly typesAssembly,
            IReadOnlyList<ExpectedObject> components,
            IReadOnlyList<ExpectedObject> parameters)
        {
            Spec = spec;
            PluginAssembly = pluginAssembly;
            TypesAssembly = typesAssembly;
            Components = components;
            Parameters = parameters;
        }

        internal DragonModuleSpec Spec { get; }

        internal Assembly PluginAssembly { get; }

        internal Assembly TypesAssembly { get; }

        internal IReadOnlyList<ExpectedObject> Components { get; }

        internal IReadOnlyList<ExpectedObject> Parameters { get; }

        internal IReadOnlyList<ExpectedObject> All => Components.Concat(Parameters).ToArray();
    }

    private sealed class PersistenceTarget
    {
        internal PersistenceTarget(ModuleCatalog catalog, Guid instanceGuid)
        {
            Catalog = catalog;
            InstanceGuid = instanceGuid;
        }

        internal ModuleCatalog Catalog { get; }

        internal Guid InstanceGuid { get; }
    }
}
