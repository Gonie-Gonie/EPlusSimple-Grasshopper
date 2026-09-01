using System.Collections;
using System.Globalization;
using System.Reflection;
#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Dragons.ComponentCatalog;

internal static class Program
{
    private const string CatalogSchema = "dragons.component-catalog.v1";
    private const string DefaultConfiguration = "Release";
    private const string DefaultFramework = "net8.0-windows";

    private static readonly JsonSerializerOptions CatalogJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private static readonly ProductSpec[] ProductSpecs =
    {
        new(
            "InvisibleDragon",
            "Dragons.InvisibleDragon.GH",
            "Dragons.InvisibleDragon.Grasshopper.Types",
            "Dragons.InvisibleDragon.Grasshopper.Parameters"),
        new(
            "SimpleDragon",
            "Dragons.SimpleDragon.GH",
            "Dragons.SimpleDragon.Grasshopper.Types",
            "Dragons.SimpleDragon.Grasshopper.Parameters"),
    };

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            string repositoryRoot = options.RepositoryRoot ?? FindRepositoryRoot();
            string configuration = options.Configuration ?? DefaultConfiguration;
            string framework = options.Framework ?? DefaultFramework;
            string outputPath = options.OutputPath
                ?? throw new ArgumentException("--output is required.");

            string[] pluginDirectories = ProductSpecs
                .Select(spec => PluginDirectory(repositoryRoot, spec.PluginProject, configuration, framework))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            RegisterPluginDependencyResolver(pluginDirectories);

            ProductCatalog[] products = ProductSpecs
                .Select(spec => BuildProduct(repositoryRoot, configuration, framework, spec))
                .ToArray();
            Require(products.Sum(product => product.Components.Count) > 0, "No public components were discovered.");
            Require(
                products.SelectMany(product => product.Components)
                    .Select(component => component.Guid)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == products.Sum(product => product.Components.Count),
                "Component GUIDs are not unique across the two products.");

            var catalog = new Catalog(
                CatalogSchema,
                framework,
                products.Sum(product => product.Components.Count),
                products.Sum(product => product.Parameters.Count),
                products);
            WriteCatalog(outputPath, catalog);
            Console.WriteLine(
                $"Wrote {catalog.ComponentCount} components and {catalog.ParameterCount} typed parameters to {Path.GetFullPath(outputPath)}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static ProductCatalog BuildProduct(
        string repositoryRoot,
        string configuration,
        string framework,
        ProductSpec spec)
    {
        string directory = PluginDirectory(repositoryRoot, spec.PluginProject, configuration, framework);
        string pluginPath = Path.Combine(directory, spec.PluginProject + ".gha");
        string typesPath = Path.Combine(directory, spec.TypesAssembly + ".dll");
        Require(File.Exists(pluginPath), "Built Grasshopper assembly is missing: " + pluginPath);
        Require(File.Exists(typesPath), "Built Grasshopper types assembly is missing: " + typesPath);

        Assembly pluginAssembly = Assembly.LoadFrom(pluginPath);
        Assembly typesAssembly = Assembly.LoadFrom(typesPath);
        Require(
            string.Equals(pluginAssembly.GetName().Name, spec.PluginProject, StringComparison.Ordinal),
            "Unexpected plugin assembly identity: " + pluginAssembly.FullName);
        Require(
            string.Equals(typesAssembly.GetName().Name, spec.TypesAssembly, StringComparison.Ordinal),
            "Unexpected types assembly identity: " + typesAssembly.FullName);

        ComponentEntry[] components = PublicTypes(pluginAssembly)
            .Where(type => typeof(GH_Component).IsAssignableFrom(type))
            .Select(type => Create<GH_Component>(type))
            .OrderBy(component => component.SubCategory, StringComparer.Ordinal)
            .ThenBy(component => component.Name, StringComparer.Ordinal)
            .ThenBy(component => component.ComponentGuid)
            .Select(component => Component(spec.Product, component))
            .ToArray();
        ParameterEntry[] parameters = PublicTypes(typesAssembly)
            .Where(type => typeof(IGH_Param).IsAssignableFrom(type)
                && string.Equals(type.Namespace, spec.ParameterNamespace, StringComparison.Ordinal))
            .Select(type => Create<IGH_Param>(type))
            .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
            .ThenBy(parameter => parameter.ComponentGuid)
            .Select(parameter => StandaloneParameter(spec.Product, parameter))
            .ToArray();

        Require(components.Length > 0, $"No public {spec.Product} components were discovered.");
        Require(parameters.Length > 0, $"No public {spec.Product} typed parameters were discovered.");
        Require(
            components.Select(component => component.Guid).Distinct(StringComparer.Ordinal).Count() == components.Length,
            spec.Product + " component GUIDs are not unique.");
        Require(
            parameters.Select(parameter => parameter.Guid).Distinct(StringComparer.Ordinal).Count() == parameters.Length,
            spec.Product + " parameter GUIDs are not unique.");
        return new ProductCatalog(spec.Product, components, parameters);
    }

    private static IEnumerable<Type> PublicTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes().Where(type =>
                type.IsPublic
                && !type.IsAbstract
                && !type.ContainsGenericParameters);
        }
        catch (ReflectionTypeLoadException exception)
        {
            string loaderDetails = string.Join(
                Environment.NewLine,
                exception.LoaderExceptions
                    .Where(item => item is not null)
                    .Select(item => item!.ToString()));
            throw new InvalidOperationException(
                $"Could not reflect every type from {assembly.Location}.{Environment.NewLine}{loaderDetails}",
                exception);
        }
    }

    private static T Create<T>(Type type)
        where T : class =>
        Activator.CreateInstance(type) as T
        ?? throw new InvalidOperationException("Could not construct " + type.FullName);

    private static ComponentEntry Component(string product, GH_Component component)
    {
        RequireText(component.Name, component.GetType(), "Name");
        RequireText(component.NickName, component.GetType(), "NickName");
        RequireText(component.Description, component.GetType(), "Description");
        RequireText(component.Category, component.GetType(), "Category");
        RequireText(component.SubCategory, component.GetType(), "SubCategory");
        Require(component.ComponentGuid != Guid.Empty, "Component has an empty GUID: " + component.GetType().FullName);

        PortEntry[] inputs = component.Params.Input
            .Select((parameter, index) => Port(parameter, index, isInput: true))
            .ToArray();
        PortEntry[] outputs = component.Params.Output
            .Select((parameter, index) => Port(parameter, index, isInput: false))
            .ToArray();
        return new ComponentEntry(
            product,
            component.GetType().FullName ?? component.GetType().Name,
            component.ComponentGuid.ToString("D", CultureInfo.InvariantCulture),
            component.Name,
            component.NickName,
            component.Description,
            component.Category,
            component.SubCategory,
            component.Exposure.ToString(),
            inputs,
            outputs);
    }

    private static PortEntry Port(IGH_Param parameter, int index, bool isInput)
    {
        Type runtimeType = parameter.GetType();
        RequireText(parameter.Name, runtimeType, "Name");
        RequireText(parameter.NickName, runtimeType, "NickName");
        RequireText(parameter.Description, runtimeType, "Description");
        RequireText(parameter.TypeName, runtimeType, "TypeName");

        string[] defaultValues = PersistentValues(parameter).Select(FormatValue).ToArray();
        ChoiceEntry[] choices = Choices(parameter)
            .Select(value => new ChoiceEntry(value, Humanize(value)))
            .ToArray();
#if NETFRAMEWORK
        bool isChoiceStringParameter =
            runtimeType.Name.IndexOf("ChoiceStringParam", StringComparison.Ordinal) >= 0;
#else
        bool isChoiceStringParameter =
            runtimeType.Name.Contains("ChoiceStringParam", StringComparison.Ordinal);
#endif
        if (isChoiceStringParameter)
        {
            Require(choices.Length > 0, "Could not extract choices from " + runtimeType.FullName);
            Require(
                defaultValues.Length == 1 && choices.Any(choice => choice.Value == defaultValues[0]),
                "A choice input default is absent from its allowed values: " + parameter.Name);
        }

        return new PortEntry(
            index,
            parameter.Name,
            parameter.NickName,
            parameter.Description,
            parameter.TypeName,
            runtimeType.FullName ?? runtimeType.Name,
            parameter.Access.ToString().ToLowerInvariant(),
            isInput ? parameter.Optional : null,
            defaultValues.Length > 0,
            defaultValues,
            choices);
    }

    private static ParameterEntry StandaloneParameter(string product, IGH_Param parameter)
    {
        Type runtimeType = parameter.GetType();
        RequireText(parameter.Name, runtimeType, "Name");
        RequireText(parameter.NickName, runtimeType, "NickName");
        RequireText(parameter.Description, runtimeType, "Description");
        RequireText(parameter.TypeName, runtimeType, "TypeName");
        Require(parameter.ComponentGuid != Guid.Empty, "Parameter has an empty GUID: " + runtimeType.FullName);
        return new ParameterEntry(
            product,
            runtimeType.FullName ?? runtimeType.Name,
            parameter.ComponentGuid.ToString("D", CultureInfo.InvariantCulture),
            parameter.Name,
            parameter.NickName,
            parameter.Description,
            parameter.TypeName,
            parameter.Category,
            parameter.SubCategory,
            parameter.Exposure.ToString());
    }

    private static object?[] PersistentValues(IGH_Param parameter)
    {
        PropertyInfo? property = parameter.GetType().GetProperty("PersistentData");
        object? structure = property?.GetValue(parameter);
        MethodInfo? allData = structure?.GetType().GetMethod(
            "AllData",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(bool) },
            modifiers: null);
        IEnumerable? data = allData?.Invoke(structure, new object[] { true }) as IEnumerable;
        return data?
            .Cast<object?>()
            .Select(value => value is IGH_Goo goo ? goo.ScriptVariable() : value)
            .ToArray()
            ?? Array.Empty<object?>();
    }

    private static string[] Choices(IGH_Param parameter)
    {
        Type? current = parameter.GetType();
        while (current is not null)
        {
            FieldInfo? field = current.GetField(
                "_allowedValues",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field?.GetValue(parameter) is string[] values)
            {
                return values.ToArray();
            }
            current = current.BaseType;
        }
        return Array.Empty<string>();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            string text => text,
            bool boolean => boolean ? "True" : "False",
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            Plane plane when plane == Plane.WorldXY => "World XY",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string Humanize(string value)
    {
        var result = new StringBuilder(value.Length + 8);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            char previous = index == 0 ? '\0' : value[index - 1];
            char next = index + 1 < value.Length ? value[index + 1] : '\0';
            bool boundary = index > 0
                && (char.IsUpper(current) && (char.IsLower(previous) || char.IsUpper(previous) && char.IsLower(next))
                    || char.IsDigit(current) && !char.IsDigit(previous));
            if (boundary)
            {
                result.Append(' ');
            }
            result.Append(current);
        }
        return result.ToString();
    }

    private static void RegisterPluginDependencyResolver(IEnumerable<string> directories)
    {
        string[] roots = directories.Select(Path.GetFullPath).ToArray();
#if NETFRAMEWORK
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var name = new AssemblyName(args.Name);
            foreach (string root in roots)
            {
                foreach (string extension in new[] { ".dll", ".gha" })
                {
                    string candidate = Path.Combine(root, name.Name + extension);
                    if (File.Exists(candidate))
                    {
                        return Assembly.LoadFrom(candidate);
                    }
                }
            }
            return null;
        };
#else
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            foreach (string root in roots)
            {
                foreach (string extension in new[] { ".dll", ".gha" })
                {
                    string candidate = Path.Combine(root, name.Name + extension);
                    if (File.Exists(candidate))
                    {
                        return context.LoadFromAssemblyPath(candidate);
                    }
                }
            }
            return null;
        };
#endif
    }

    private static string PluginDirectory(
        string repositoryRoot,
        string project,
        string configuration,
        string framework) =>
        Path.Combine(repositoryRoot, "temp", "build", "bin", project, configuration, framework);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "global.json")))
        {
            current = current.Parent;
        }
        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root from the catalog executable.");
    }

    private static void WriteCatalog(string path, Catalog catalog)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(
            fullPath,
            JsonSerializer.Serialize(catalog, CatalogJsonOptions) + "\n",
            new UTF8Encoding(false));
    }

    private static void RequireText(string? value, Type owner, string property)
    {
        Require(!string.IsNullOrWhiteSpace(value), $"{owner.FullName}.{property} is blank.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record ProductSpec(
        string Product,
        string PluginProject,
        string TypesAssembly,
        string ParameterNamespace);

    private sealed record Catalog(
        string Schema,
        string Framework,
        int ComponentCount,
        int ParameterCount,
        IReadOnlyList<ProductCatalog> Products);

    private sealed record ProductCatalog(
        string Product,
        IReadOnlyList<ComponentEntry> Components,
        IReadOnlyList<ParameterEntry> Parameters);

    private sealed record ComponentEntry(
        string Product,
        string RuntimeType,
        string Guid,
        string Name,
        string Nickname,
        string Description,
        string Category,
        string Subcategory,
        string Exposure,
        IReadOnlyList<PortEntry> Inputs,
        IReadOnlyList<PortEntry> Outputs);

    private sealed record PortEntry(
        int Index,
        string Name,
        string Nickname,
        string Description,
        string FriendlyType,
        string RuntimeType,
        string Access,
        bool? Optional,
        bool HasPersistentDefault,
        IReadOnlyList<string> DefaultValues,
        IReadOnlyList<ChoiceEntry> Choices);

    private sealed record ChoiceEntry(string Value, string Label);

    private sealed record ParameterEntry(
        string Product,
        string RuntimeType,
        string Guid,
        string Name,
        string Nickname,
        string Description,
        string FriendlyType,
        string Category,
        string Subcategory,
        string Exposure);

    private sealed class Options
    {
        internal string? RepositoryRoot { get; private set; }

        internal string? OutputPath { get; private set; }

        internal string? Configuration { get; private set; }

        internal string? Framework { get; private set; }

        internal static Options Parse(IReadOnlyList<string> args)
        {
            var options = new Options();
            for (int index = 0; index < args.Count; index++)
            {
                string name = args[index];
                string value = index + 1 < args.Count
                    ? args[++index]
                    : throw new ArgumentException("Missing value for " + name);
                switch (name)
                {
                    case "--repository-root":
                        options.RepositoryRoot = Path.GetFullPath(value);
                        break;
                    case "--output":
                        options.OutputPath = Path.GetFullPath(value);
                        break;
                    case "--configuration":
                        options.Configuration = value;
                        break;
                    case "--framework":
                        options.Framework = value;
                        break;
                    default:
                        throw new ArgumentException("Unknown argument: " + name);
                }
            }
            return options;
        }
    }
}
