using System.Reflection;
using Grasshopper.Kernel;

namespace GonieGonie.Dragons.Grasshopper.Rhino7Probe;

internal static class Rhino7Gate
{
    private static readonly Guid InvisibleVersionGuid = new("bcdd73c6-e40f-4ae4-9b9b-5dc78a238b18");
    private static readonly Guid DiagnosticParamGuid = new("84cffc02-1023-428b-b96a-e327b5a73c65");
    private const string SimpleMaterialName = "Smoke Simple Material";
    private const string SimpleMaterialGooTypeName = "GonieGonie.SimpleDragon.Grasshopper.Types.SimpleDragonMaterialGoo";
    private const string SimpleMaterialParamTypeName = "GonieGonie.SimpleDragon.Grasshopper.Parameters.SimpleDragonMaterialParam";

    private static readonly Guid[] InvisibleComponentIds =
    {
        InvisibleVersionGuid,
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
        DiagnosticParamGuid,
    };

    private static readonly Guid[] BasicDocumentComponentIds =
    {
        InvisibleVersionGuid,
        new("e292a44e-9d8d-4796-95fb-126f77e83796"),
        new("3d5717de-1b16-406a-91e0-7a392c08aa51"),
        new("e5627899-dcdb-4154-98fc-f7c547d50d2e"),
        new("fee2629c-94d8-4eed-8be2-14ba108ce825"),
        new("2743be88-ef3a-4f0d-abf8-cf062d93aafe"),
        new("fa664eeb-5503-4366-831d-e3478c8a1832"),
    };

    public static int RunHosted(string invisibleGha, string simpleGha, string outputPath)
    {
        Console.WriteLine($"hosted-rhino={Rhino.RhinoApp.Version}");
        Require(Rhino.RhinoApp.Version.Major == 7, "Rhino.Inside did not load Rhino 7.");
        GH_ComponentServer server = global::Grasshopper.Instances.ComponentServer;
        string invisibleTypesPath = Path.Combine(
            Path.GetDirectoryName(invisibleGha)!,
            "GonieGonie.InvisibleDragon.Grasshopper.Types.dll");
        string simpleTypesPath = Path.Combine(
            Path.GetDirectoryName(simpleGha)!,
            "GonieGonie.SimpleDragon.Grasshopper.Types.dll");
        Parse(server, invisibleGha);
        Assembly simplePlugin = Parse(server, simpleGha);
        Parse(server, invisibleTypesPath);
        Assembly simpleTypes = Parse(server, simpleTypesPath);
        SimplePluginCatalog simpleCatalog = DiscoverSimplePlugin(simplePlugin, simpleTypes);

        var registered = new HashSet<Guid>(server.ObjectProxies.Select(proxy => proxy.Guid));
        foreach (Guid id in InvisibleComponentIds)
        {
            Require(registered.Contains(id), $"InvisibleDragon component proxy {id} was not registered.");
            RequireObject(server.EmitObject(id), $"InvisibleDragon component {id}");
        }

        foreach (Guid id in InvisibleParameterIds)
        {
            Require(registered.Contains(id), $"InvisibleDragon parameter proxy {id} was not registered.");
            RequireObject(server.EmitObject(id), $"InvisibleDragon parameter {id}");
        }

        AssertRegistered(server, registered, simpleCatalog.Components, "SimpleDragon component");
        AssertRegistered(server, registered, simpleCatalog.Parameters, "SimpleDragon parameter");

        var document = new GH_Document();
        foreach (Guid id in BasicDocumentComponentIds)
        {
            document.AddObject(RequireObject(server.EmitObject(id), $"InvisibleDragon component {id}"), false, 0);
        }

        foreach (ExpectedObject component in simpleCatalog.Components)
        {
            document.AddObject(Emit(server, component), false, document.ObjectCount);
        }

        IGH_DocumentObject diagnosticParameter = RequireObject(
            server.EmitObject(DiagnosticParamGuid),
            "Diagnostic parameter");
        AddDiagnosticPersistentData(diagnosticParameter, invisibleGha);
        document.AddObject(diagnosticParameter, false, document.ObjectCount);
        Guid diagnosticParameterInstanceGuid = diagnosticParameter.InstanceGuid;

        Guid simpleMaterialParameterInstanceGuid = Guid.Empty;
        foreach (ExpectedObject parameter in simpleCatalog.Parameters)
        {
            IGH_DocumentObject instance = Emit(server, parameter);
            if (string.Equals(parameter.RuntimeType.FullName, SimpleMaterialParamTypeName, StringComparison.Ordinal))
            {
                AddSimpleMaterialPersistentData(instance, simpleGha);
                simpleMaterialParameterInstanceGuid = instance.InstanceGuid;
            }

            document.AddObject(instance, false, document.ObjectCount);
        }

        Require(simpleMaterialParameterInstanceGuid != Guid.Empty,
            "SimpleDragon material parameter discovery did not yield a persistence target.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var writer = new GH_DocumentIO(document);
        Require(writer.SaveQuiet(outputPath), "Grasshopper document save failed.");
        var reader = new GH_DocumentIO();
        Require(reader.Open(outputPath), "Grasshopper document reopen failed.");
        GH_Document reopened = reader.Document;
        Require(reopened.Objects.Count == document.Objects.Count,
            $"Expected {document.Objects.Count} reopened objects; got {reopened.Objects.Count}.");
        foreach (Guid id in BasicDocumentComponentIds)
        {
            Require(reopened.Objects.Any(item => item.ComponentGuid == id), $"Reopened document lost component {id}.");
        }

        foreach (ExpectedObject expected in simpleCatalog.All)
        {
            Require(reopened.Objects.Any(item => item.ComponentGuid == expected.Id),
                $"Reopened document lost {expected.RuntimeType.FullName} ({expected.Id}).");
        }

        VerifyDiagnosticPersistentData(
            reopened.Objects.Single(item => item.InstanceGuid == diagnosticParameterInstanceGuid));
        (string simpleGooType, string simpleMaterialName) = ReadNamedPersistentData(
            reopened.Objects.Single(item => item.InstanceGuid == simpleMaterialParameterInstanceGuid));
        Require(string.Equals(simpleMaterialName, SimpleMaterialName, StringComparison.Ordinal),
            "The SimpleDragon Goo domain value changed during Grasshopper save/reopen.");
        File.WriteAllText(
            outputPath + ".summary.txt",
            $"Rhino={Rhino.RhinoApp.Version}{Environment.NewLine}" +
            $"InvisibleComponents={InvisibleComponentIds.Length}{Environment.NewLine}" +
            $"InvisibleParameters={InvisibleParameterIds.Length}{Environment.NewLine}" +
            $"SimpleComponents={simpleCatalog.Components.Count}{Environment.NewLine}" +
            $"SimpleParameters={simpleCatalog.Parameters.Count}{Environment.NewLine}" +
            $"ReopenedObjects={reopened.Objects.Count}{Environment.NewLine}" +
            "GooCode=RHINO7_HOST_GATE" + Environment.NewLine +
            $"SimpleGooType={simpleGooType}{Environment.NewLine}" +
            $"SimpleGooName={simpleMaterialName}{Environment.NewLine}");
        Console.WriteLine(
            $"hosted-proxy-registration=ok (InvisibleDragon {InvisibleComponentIds.Length}+{InvisibleParameterIds.Length}; " +
            $"SimpleDragon {simpleCatalog.Components.Count}+{simpleCatalog.Parameters.Count}); " +
            $"hosted-save-reopen=ok ({reopened.Objects.Count}); hosted-simple-goo-roundtrip=ok");
        return 0;
    }

    private static Assembly Parse(GH_ComponentServer server, string path)
    {
        Assembly assembly = Assembly.LoadFrom(path);
        MethodInfo parser = typeof(GH_ComponentServer).GetMethod(
            "ParseGHA",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(Assembly), typeof(string) },
            modifiers: null)
            ?? throw new MissingMethodException(typeof(GH_ComponentServer).FullName, "ParseGHA(Assembly, string)");
        parser.Invoke(server, new object[] { assembly, path });
        return assembly;
    }

    private static void AddDiagnosticPersistentData(IGH_DocumentObject parameter, string invisibleGha)
    {
        string directory = Path.GetDirectoryName(invisibleGha)!;
        Assembly types = Assembly.LoadFrom(Path.Combine(directory, "GonieGonie.InvisibleDragon.Grasshopper.Types.dll"));

        Type gooType = types.GetType("GonieGonie.InvisibleDragon.Grasshopper.Types.DiagnosticGoo", true)!;
        ConstructorInfo gooConstructor = RequireValueConstructor(gooType);
        Type diagnosticType = gooConstructor.GetParameters()[0].ParameterType;
        Type severityType = diagnosticType.Assembly.GetType(
            "GonieGonie.BuildingEnergy.Contracts.DiagnosticSeverity",
            true)!;
        object severity = Enum.Parse(severityType, "Info");
        ConstructorInfo constructor = diagnosticType.GetConstructors().Single();
        object diagnostic = constructor.Invoke(new object?[]
        {
            "RHINO7_HOST_GATE",
            severity,
            "Rhino 7 hosted Grasshopper persistence gate.",
            null,
            null,
            "Retain this value after save/reopen."
        });

        object goo = gooConstructor.Invoke(new[] { diagnostic });
        MethodInfo addPersistentData = parameter.GetType().GetMethod("AddPersistentData", new[] { typeof(object) })
            ?? throw new MissingMethodException(parameter.GetType().FullName, "AddPersistentData(object)");
        Require((bool)addPersistentData.Invoke(parameter, new[] { goo })!, "Adding persistent Goo failed.");
    }

    private static void AddSimpleMaterialPersistentData(IGH_DocumentObject parameter, string simpleGha)
    {
        string directory = Path.GetDirectoryName(simpleGha)!;
        Assembly types = Assembly.LoadFrom(Path.Combine(directory, "GonieGonie.SimpleDragon.Grasshopper.Types.dll"));
        Type gooType = types.GetType(SimpleMaterialGooTypeName, true)!;
        ConstructorInfo gooConstructor = RequireValueConstructor(gooType);
        Type materialType = gooConstructor.GetParameters()[0].ParameterType;
        object material = Activator.CreateInstance(
            materialType,
            new object?[] { SimpleMaterialName, 0.04, 30.0, 1400.0, null })!;
        object goo = gooConstructor.Invoke(new[] { material });
        MethodInfo addPersistentData = parameter.GetType().GetMethod(
            "AddPersistentData",
            new[] { typeof(object) })
            ?? throw new MissingMethodException(parameter.GetType().FullName, "AddPersistentData(object)");
        Require((bool)addPersistentData.Invoke(parameter, new[] { goo })!,
            "Adding SimpleDragon persistent Goo failed.");
    }

    private static ConstructorInfo RequireValueConstructor(Type gooType)
    {
        return gooType.GetConstructors()
            .Single(constructor => constructor.GetParameters().Length == 1);
    }

    private static void VerifyDiagnosticPersistentData(IGH_DocumentObject parameter)
    {
        PropertyInfo countProperty = parameter.GetType().GetProperty("PersistentDataCount")
            ?? throw new MissingMemberException(parameter.GetType().FullName, "PersistentDataCount");
        int count = (int)countProperty.GetValue(parameter)!;
        Require(count == 1, $"Expected one persistent Goo item after reopen; got {count}.");

        PropertyInfo dataProperty = parameter.GetType().GetProperty("PersistentData")
            ?? throw new MissingMemberException(parameter.GetType().FullName, "PersistentData");
        object tree = dataProperty.GetValue(parameter)!;
        MethodInfo allDataMethod = tree.GetType().GetMethod("AllData", new[] { typeof(bool) })
            ?? throw new MissingMethodException(tree.GetType().FullName, "AllData(bool)");
        var allData = (System.Collections.IEnumerable)allDataMethod.Invoke(tree, new object[] { false })!;
        object goo = allData.Cast<object>().Single();
        object diagnostic = goo.GetType().GetMethod("ScriptVariable")!.Invoke(goo, null)!;
        string code = (string)diagnostic.GetType().GetProperty("Code")!.GetValue(diagnostic)!;
        Require(code == "RHINO7_HOST_GATE", $"Unexpected reopened Goo code: {code}");
    }

    private static (string GooType, string ValueName) ReadNamedPersistentData(IGH_DocumentObject parameter)
    {
        PropertyInfo countProperty = parameter.GetType().GetProperty("PersistentDataCount")
            ?? throw new MissingMemberException(parameter.GetType().FullName, "PersistentDataCount");
        int count = (int)countProperty.GetValue(parameter)!;
        Require(count == 1, $"Expected one SimpleDragon Goo item after reopen; got {count}.");

        PropertyInfo dataProperty = parameter.GetType().GetProperty("PersistentData")
            ?? throw new MissingMemberException(parameter.GetType().FullName, "PersistentData");
        object tree = dataProperty.GetValue(parameter)!;
        MethodInfo allDataMethod = tree.GetType().GetMethod("AllData", new[] { typeof(bool) })
            ?? throw new MissingMethodException(tree.GetType().FullName, "AllData(bool)");
        var allData = (System.Collections.IEnumerable)allDataMethod.Invoke(tree, new object[] { false })!;
        object goo = allData.Cast<object>().Single();
        object domain = goo.GetType().GetMethod("ScriptVariable")!.Invoke(goo, null)!;
        string name = (string)domain.GetType().GetProperty("Name")!.GetValue(domain)!;
        return (goo.GetType().FullName ?? goo.GetType().Name, name);
    }

    private static void AssertRegistered(
        GH_ComponentServer server,
        HashSet<Guid> registered,
        IEnumerable<ExpectedObject> expectedObjects,
        string label)
    {
        foreach (ExpectedObject expected in expectedObjects)
        {
            Require(registered.Contains(expected.Id),
                $"{label} proxy {expected.RuntimeType.FullName} ({expected.Id}) was not registered.");
            _ = Emit(server, expected);
        }
    }

    private static IGH_DocumentObject Emit(GH_ComponentServer server, ExpectedObject expected)
    {
        IGH_DocumentObject emitted = RequireObject(
            server.EmitObject(expected.Id),
            expected.RuntimeType.FullName ?? expected.Id.ToString());
        Require(string.Equals(
                emitted.GetType().FullName,
                expected.RuntimeType.FullName,
                StringComparison.Ordinal),
            $"Grasshopper emitted '{emitted.GetType().FullName}' for '{expected.RuntimeType.FullName}'.");
        return emitted;
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
        Require(components.Length > 0, "No public SimpleDragon components were discovered.");
        Require(parameters.Length > 0, "No public SimpleDragon parameters were discovered.");
        ExpectedObject[] all = components.Concat(parameters).ToArray();
        Require(all.Select(item => item.Id).Distinct().Count() == all.Length,
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
                Require(instance.ComponentGuid != Guid.Empty,
                    "Discovered Grasshopper object has an empty GUID: " + type.FullName);
                return new ExpectedObject(type, instance.ComponentGuid);
            })
            .OrderBy(item => item.RuntimeType.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IGH_DocumentObject RequireObject(IGH_DocumentObject? value, string label)
    {
        return value ?? throw new InvalidOperationException($"Component server did not emit {label}.");
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
        public ExpectedObject(Type runtimeType, Guid id)
        {
            RuntimeType = runtimeType;
            Id = id;
        }

        public Type RuntimeType { get; }

        public Guid Id { get; }
    }

    private sealed class SimplePluginCatalog
    {
        public SimplePluginCatalog(
            IReadOnlyList<ExpectedObject> components,
            IReadOnlyList<ExpectedObject> parameters)
        {
            Components = components;
            Parameters = parameters;
        }

        public IReadOnlyList<ExpectedObject> Components { get; }

        public IReadOnlyList<ExpectedObject> Parameters { get; }

        public IReadOnlyList<ExpectedObject> All => Components.Concat(Parameters).ToArray();
    }
}
