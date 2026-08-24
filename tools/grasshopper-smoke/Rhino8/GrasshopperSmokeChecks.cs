using System.Reflection;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace GonieGonie.Dragons.GrasshopperSmoke.Rhino8;

internal static class GrasshopperSmokeChecks
{
    private const string MaterialName = "Smoke Wall Material";
    private const string MaterialTypeName = "GonieGonie.InvisibleDragon.Construction.Material";
    private const string MaterialGooTypeName = "GonieGonie.InvisibleDragon.Grasshopper.Types.DragonMaterialGoo";
    private const string SimpleMaterialName = "Smoke Simple Material";
    private const string SimpleMaterialTypeName = "GonieGonie.SimpleDragon.Material";
    private const string SimpleMaterialGooTypeName = "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonMaterialGoo";
    private const string SimpleMaterialParamTypeName = "GonieGonie.SimpleDragon.Grasshopper.Parameters.SimpleDragonMaterialParam";

    private static readonly Guid[] InvisibleComponentIds =
    {
        new("bcdd73c6-e40f-4ae4-9b9b-5dc78a238b18"),
        new("dca742da-0ac5-4520-8022-97f98974dfea"),
        new("6d5a9b54-8a9e-4c95-91df-469e21a783c9"),
        new("e292a44e-9d8d-4796-95fb-126f77e83796"),
        new("291150ba-bbb5-41c2-99ac-914a5183d3ed"),
        new("3d5717de-1b16-406a-91e0-7a392c08aa51"),
        new("e5627899-dcdb-4154-98fc-f7c547d50d2e"),
        new("fee2629c-94d8-4eed-8be2-14ba108ce825"),
        new("2743be88-ef3a-4f0d-abf8-cf062d93aafe"),
        new("fa664eeb-5503-4366-831d-e3478c8a1832"),
        new("5f1a9663-6f81-4635-b54d-607b48c9fd47"),
        new("af9419cd-0d68-4ee2-870b-b2ac04c95a41"),
        new("31967aee-84ae-4536-b091-b301d1ab2c3d"),
    };

    private static readonly Guid[] InvisibleParameterIds =
    {
        new("02652d26-0b4e-467f-b079-c660bb7243c2"),
        new("3e7d571e-6914-47b1-b130-7bd1b2121a86"),
        new("8aa326cf-4bcb-4386-aa90-4b81a851355c"),
        new("39d3b7f4-4287-41a5-b260-d61077b88b55"),
        new("1ce3f493-c9c4-4549-893a-0a950998da62"),
        new("cff53fa0-0cc2-4c50-832e-fdf82691b9cc"),
        new("dbfba1b5-624a-4db4-8fec-d80eb9561467"),
        new("fc64602d-d9bc-4052-a563-c7f8ea77ae99"),
        new("3aded2aa-eaa9-4154-a7bc-736dd8bc783f"),
        new("84cffc02-1023-428b-b96a-e327b5a73c65"),
    };

    private static readonly Guid[] BasicDocumentComponentIds =
    {
        new("bcdd73c6-e40f-4ae4-9b9b-5dc78a238b18"),
        new("e292a44e-9d8d-4796-95fb-126f77e83796"),
        new("3d5717de-1b16-406a-91e0-7a392c08aa51"),
        new("e5627899-dcdb-4154-98fc-f7c547d50d2e"),
        new("fee2629c-94d8-4eed-8be2-14ba108ce825"),
        new("2743be88-ef3a-4f0d-abf8-cf062d93aafe"),
        new("fa664eeb-5503-4366-831d-e3478c8a1832"),
    };

    public static void RestrictExternalLibraries(IReadOnlyList<string> pluginPaths)
    {
        GH_ComponentServer server = Instances.ComponentServer;
        MethodInfo method = server.GetType().GetMethod(
            "SetExternalGHAs",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(IEnumerable<string>) },
            modifiers: null)
            ?? throw new MissingMethodException(server.GetType().FullName, "SetExternalGHAs");

        method.Invoke(null, new object[] { pluginPaths });
    }

    public static GrasshopperSmokeSummary Run(HostInputs inputs, string rhinoVersion)
    {
        GH_ComponentServer server = Instances.ComponentServer;
        string invisibleTypesPath = Path.Combine(
            Path.GetDirectoryName(inputs.InvisibleDragonGha)!,
            "GonieGonie.InvisibleDragon.Grasshopper.Types.dll");
        string simpleTypesPath = Path.Combine(
            Path.GetDirectoryName(inputs.SimpleDragonGha)!,
            "GonieGonie.SimpleDragon.Grasshopper.Types.dll");
        Assembly simplePluginAssembly = Assembly.LoadFrom(inputs.SimpleDragonGha);
        Assembly simpleTypesAssembly = Assembly.LoadFrom(simpleTypesPath);
        SimplePluginCatalog simpleCatalog = DiscoverSimplePlugin(simplePluginAssembly, simpleTypesAssembly);

        Progress("loading the requested Dragon libraries through the Grasshopper component server");
        if (!AllRegistered(server, InvisibleComponentIds))
        {
            ParseExternalLibrary(server, inputs.InvisibleDragonGha);
        }

        if (!AllRegistered(server, InvisibleParameterIds))
        {
            ParseExternalLibrary(
                server,
                invisibleTypesPath);
        }

        if (!AllRegistered(server, simpleCatalog.Components.Select(item => item.Id)))
        {
            ParseExternalLibrary(server, inputs.SimpleDragonGha);
        }

        if (!AllRegistered(server, simpleCatalog.Parameters.Select(item => item.Id)))
        {
            ParseExternalLibrary(server, simpleTypesPath);
        }

        Progress("checking registered component proxies");
        AssertRegistered(server, InvisibleComponentIds, "InvisibleDragon component");
        AssertRegistered(server, InvisibleParameterIds, "InvisibleDragon parameter");
        AssertRegistered(server, simpleCatalog.Components, "SimpleDragon component");
        AssertRegistered(server, simpleCatalog.Parameters, "SimpleDragon parameter");

        Progress("creating Dragon document with every SimpleDragon proxy and persistent Goo");
        var document = new GH_Document();
        foreach (Guid id in BasicDocumentComponentIds)
        {
            document.AddObject(Emit(server, id), update: false, index: document.ObjectCount);
        }

        IGH_DocumentObject invisibleMaterialParameter = Emit(server, InvisibleParameterIds[0]);
        AddMaterialGoo(invisibleMaterialParameter);
        Guid invisibleMaterialParameterInstanceId = invisibleMaterialParameter.InstanceGuid;
        document.AddObject(invisibleMaterialParameter, update: false, index: document.ObjectCount);

        foreach (ExpectedObject component in simpleCatalog.Components)
        {
            document.AddObject(Emit(server, component), update: false, index: document.ObjectCount);
        }

        Guid simpleMaterialParameterInstanceId = Guid.Empty;
        foreach (ExpectedObject parameter in simpleCatalog.Parameters)
        {
            IGH_DocumentObject instance = Emit(server, parameter);
            if (string.Equals(parameter.RuntimeType.FullName, SimpleMaterialParamTypeName, StringComparison.Ordinal))
            {
                AddSimpleMaterialGoo(instance);
                simpleMaterialParameterInstanceId = instance.InstanceGuid;
            }

            document.AddObject(instance, update: false, index: document.ObjectCount);
        }

        Check(simpleMaterialParameterInstanceId != Guid.Empty,
            "SimpleDragon material parameter discovery did not yield a persistence target.");

        var io = new GH_DocumentIO(document);
        Progress("saving Dragon proxy document");
        Check(io.SaveQuiet(inputs.DocumentPath), "Grasshopper did not save the smoke document.");
        Check(File.Exists(inputs.DocumentPath), "Grasshopper reported success but the smoke document is absent.");

        var reopenedIo = new GH_DocumentIO();
        Progress("reopening Dragon proxy document");
        Check(reopenedIo.Open(inputs.DocumentPath), "Grasshopper did not reopen the saved smoke document.");
        GH_Document reopened = reopenedIo.Document
            ?? throw new InvalidOperationException("Grasshopper reopened the file without a document.");
        Check(reopened.ObjectCount == document.ObjectCount, "The reopened Grasshopper object count changed.");

        foreach (Guid id in BasicDocumentComponentIds)
        {
            Check(reopened.Objects.Any(item => item.ComponentGuid == id),
                $"The reopened document lost component {id}.");
        }

        foreach (ExpectedObject expected in simpleCatalog.All)
        {
            Check(reopened.Objects.Any(item => item.ComponentGuid == expected.Id),
                $"The reopened document lost {expected.RuntimeType.FullName} ({expected.Id}).");
        }

        IGH_DocumentObject reopenedInvisibleParameter = reopened.FindObject(
                invisibleMaterialParameterInstanceId,
                topLevelOnly: true)
            ?? throw new InvalidOperationException("The reopened document lost the persistent material parameter.");
        (string invisibleGooType, string materialName) = ReadNamedGoo(reopenedInvisibleParameter);
        Check(string.Equals(materialName, MaterialName, StringComparison.Ordinal),
            "The custom Goo domain value changed during Grasshopper save/reopen.");

        IGH_DocumentObject reopenedSimpleParameter = reopened.FindObject(
                simpleMaterialParameterInstanceId,
                topLevelOnly: true)
            ?? throw new InvalidOperationException("The reopened document lost the persistent SimpleDragon material parameter.");
        (string simpleGooType, string simpleMaterialName) = ReadNamedGoo(reopenedSimpleParameter);
        Check(string.Equals(simpleMaterialName, SimpleMaterialName, StringComparison.Ordinal),
            "The SimpleDragon Goo domain value changed during Grasshopper save/reopen.");

        return new GrasshopperSmokeSummary(
            Host: "Rhino.Inside STA / Grasshopper RunHeadless",
            RhinoVersion: rhinoVersion,
            GrasshopperVersion: typeof(Instances).Assembly.GetName().Version?.ToString() ?? "unknown",
            RegisteredInvisibleComponents: InvisibleComponentIds.Length,
            RegisteredInvisibleParameters: InvisibleParameterIds.Length,
            RegisteredSimpleComponents: simpleCatalog.Components.Count,
            RegisteredSimpleParameters: simpleCatalog.Parameters.Count,
            ReopenedObjectCount: reopened.ObjectCount,
            InvisibleGooType: invisibleGooType,
            InvisibleGooValueName: materialName,
            SimpleGooType: simpleGooType,
            SimpleGooValueName: simpleMaterialName,
            DocumentPath: inputs.DocumentPath);
    }

    private static void Progress(string message)
    {
        Console.WriteLine($"[grasshopper-smoke] {message}");
        Console.Out.Flush();
    }

    private static bool AllRegistered(GH_ComponentServer server, IEnumerable<Guid> ids)
    {
        return ids.All(id => server.EmitObjectProxy(id) is not null);
    }

    private static void ParseExternalLibrary(GH_ComponentServer server, string path)
    {
        Check(File.Exists(path), $"Grasshopper library is absent: {path}");
        Assembly assembly = Assembly.LoadFrom(path);
        MethodInfo parser = server.GetType().GetMethod(
            "ParseGHA",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(Assembly), typeof(string) },
            modifiers: null)
            ?? throw new MissingMethodException(server.GetType().FullName, "ParseGHA(Assembly, string)");
        parser.Invoke(server, new object[] { assembly, path });
    }

    private static void AssertRegistered(GH_ComponentServer server, IEnumerable<Guid> ids, string kind)
    {
        foreach (Guid id in ids)
        {
            Check(server.EmitObjectProxy(id) is not null, $"Grasshopper did not register {kind} {id}.");
            _ = Emit(server, id);
        }
    }

    private static void AssertRegistered(
        GH_ComponentServer server,
        IEnumerable<ExpectedObject> expectedObjects,
        string kind)
    {
        foreach (ExpectedObject expected in expectedObjects)
        {
            Check(server.EmitObjectProxy(expected.Id) is not null,
                $"Grasshopper did not register {kind} {expected.RuntimeType.FullName} ({expected.Id}).");
            _ = Emit(server, expected);
        }
    }

    private static IGH_DocumentObject Emit(GH_ComponentServer server, Guid id)
    {
        return server.EmitObject(id)
            ?? throw new InvalidOperationException($"Grasshopper could not instantiate registered object {id}.");
    }

    private static IGH_DocumentObject Emit(GH_ComponentServer server, ExpectedObject expected)
    {
        IGH_DocumentObject emitted = Emit(server, expected.Id);
        Check(string.Equals(
                emitted.GetType().FullName,
                expected.RuntimeType.FullName,
                StringComparison.Ordinal),
            $"Grasshopper emitted '{emitted.GetType().FullName}' for '{expected.RuntimeType.FullName}'.");
        return emitted;
    }

    private static void AddMaterialGoo(IGH_DocumentObject parameter)
    {
        Assembly coreAssembly = RequireLoadedAssembly("GonieGonie.InvisibleDragon.Core");
        Assembly typesAssembly = RequireLoadedAssembly("GonieGonie.InvisibleDragon.Grasshopper.Types");
        Type materialType = coreAssembly.GetType(MaterialTypeName, throwOnError: true)!;
        Type roughnessType = coreAssembly.GetType(
            "GonieGonie.InvisibleDragon.Construction.MaterialRoughness",
            throwOnError: true)!;
        object roughness = Enum.Parse(roughnessType, "Rough", ignoreCase: false);
        object material = Activator.CreateInstance(
            materialType,
            new[] { MaterialName, 0.72, 1900.0, 840.0, 0.9, 0.7, 0.7, roughness })
            ?? throw new InvalidOperationException("Could not construct the material test value.");

        Type gooType = typesAssembly.GetType(MaterialGooTypeName, throwOnError: true)!;
        object goo = Activator.CreateInstance(gooType, new[] { material })
            ?? throw new InvalidOperationException("Could not construct the material Goo test value.");
        MethodInfo add = parameter.GetType().GetMethod(
            "AddPersistentData",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(object) },
            modifiers: null)
            ?? throw new MissingMethodException(parameter.GetType().FullName, "AddPersistentData(object)");
        bool added = add.Invoke(parameter, new[] { goo }) as bool? ?? false;
        Check(added, "The InvisibleDragon parameter rejected its matching Goo value.");
    }

    private static void AddSimpleMaterialGoo(IGH_DocumentObject parameter)
    {
        Assembly coreAssembly = RequireLoadedAssembly("GonieGonie.SimpleDragon.Core");
        Assembly typesAssembly = RequireLoadedAssembly("GonieGonie.SimpleDragon.Grasshopper.Types");
        Type materialType = coreAssembly.GetType(SimpleMaterialTypeName, throwOnError: true)!;
        object material = Activator.CreateInstance(
            materialType,
            new object?[] { SimpleMaterialName, 0.04, 30.0, 1400.0, null })
            ?? throw new InvalidOperationException("Could not construct the SimpleDragon material test value.");
        Type gooType = typesAssembly.GetType(SimpleMaterialGooTypeName, throwOnError: true)!;
        object goo = Activator.CreateInstance(gooType, new[] { material })
            ?? throw new InvalidOperationException("Could not construct the SimpleDragon material Goo test value.");
        MethodInfo add = parameter.GetType().GetMethod(
            "AddPersistentData",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(object) },
            modifiers: null)
            ?? throw new MissingMethodException(parameter.GetType().FullName, "AddPersistentData(object)");
        bool added = add.Invoke(parameter, new[] { goo }) as bool? ?? false;
        Check(added, "The SimpleDragon parameter rejected its matching Goo value.");
    }

    private static (string GooType, string ValueName) ReadNamedGoo(IGH_DocumentObject parameter)
    {
        PropertyInfo countProperty = parameter.GetType().GetProperty("PersistentDataCount")
            ?? throw new MissingMemberException(parameter.GetType().FullName, "PersistentDataCount");
        int count = countProperty.GetValue(parameter) as int? ?? 0;
        Check(count == 1, $"The reopened persistent parameter contains {count} values instead of one.");

        PropertyInfo dataProperty = parameter.GetType().GetProperty("PersistentData")
            ?? throw new MissingMemberException(parameter.GetType().FullName, "PersistentData");
        object data = dataProperty.GetValue(parameter)
            ?? throw new InvalidOperationException("The reopened parameter has no persistent data tree.");
        MethodInfo firstItem = data.GetType().GetMethod("get_FirstItem", new[] { typeof(bool) })
            ?? throw new MissingMethodException(data.GetType().FullName, "get_FirstItem(bool)");
        object gooObject = firstItem.Invoke(data, new object[] { false })
            ?? throw new InvalidOperationException("The reopened persistent data tree contains no Goo.");
        Check(gooObject is IGH_Goo, "The reopened persistent value does not implement IGH_Goo.");

        object domain = ((IGH_Goo)gooObject).ScriptVariable()
            ?? throw new InvalidOperationException("The reopened Goo has no domain value.");
        string name = domain.GetType().GetProperty("Name")?.GetValue(domain) as string
            ?? throw new MissingMemberException(domain.GetType().FullName, "Name");
        return (gooObject.GetType().FullName ?? gooObject.GetType().Name, name);
    }

    private static SimplePluginCatalog DiscoverSimplePlugin(
        Assembly componentAssembly,
        Assembly parameterAssembly)
    {
        ExpectedObject[] components = Discover(
            componentAssembly,
            type => typeof(GH_Component).IsAssignableFrom(type));
        ExpectedObject[] parameters = Discover(
            parameterAssembly,
            type => typeof(IGH_Param).IsAssignableFrom(type)
                && type.Namespace is not null
                && type.Namespace.StartsWith(
                    "GonieGonie.SimpleDragon.Grasshopper.Parameters",
                    StringComparison.Ordinal));
        Check(components.Length > 0, "No public SimpleDragon components were discovered.");
        Check(parameters.Length > 0, "No public SimpleDragon parameters were discovered.");
        ExpectedObject[] all = components.Concat(parameters).ToArray();
        Check(all.Select(item => item.Id).Distinct().Count() == all.Length,
            "SimpleDragon component and parameter GUIDs are not unique.");
        return new SimplePluginCatalog(components, parameters);
    }

    private static ExpectedObject[] Discover(Assembly assembly, Func<Type, bool> predicate)
    {
        return assembly.GetTypes()
            .Where(type => type.IsPublic
                && !type.IsAbstract
                && !type.ContainsGenericParameters
                && predicate(type))
            .Select(type =>
            {
                var instance = Activator.CreateInstance(type) as IGH_DocumentObject
                    ?? throw new InvalidOperationException(
                        "Could not construct discovered Grasshopper object '" + type.FullName + "'.");
                Check(instance.ComponentGuid != Guid.Empty,
                    "Discovered Grasshopper object has an empty GUID: " + type.FullName);
                return new ExpectedObject(type, instance.ComponentGuid);
            })
            .OrderBy(item => item.RuntimeType.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static Assembly RequireLoadedAssembly(string simpleName)
    {
        return AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(
                assembly => string.Equals(assembly.GetName().Name, simpleName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Grasshopper did not load dependency assembly '{simpleName}'.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record ExpectedObject(Type RuntimeType, Guid Id);

    private sealed record SimplePluginCatalog(
        IReadOnlyList<ExpectedObject> Components,
        IReadOnlyList<ExpectedObject> Parameters)
    {
        public IReadOnlyList<ExpectedObject> All => Components.Concat(Parameters).ToArray();
    }
}
