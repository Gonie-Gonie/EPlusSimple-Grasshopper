using System.Reflection;
using System.Security.Cryptography;
using Dragons.InvisibleDragon.Grasshopper.Parameters;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace Dragons.InvisibleDragon.Grasshopper.Tests;

public sealed class GrasshopperAssemblyTests
{
    private static readonly Guid[] HvacComponentGuids =
    {
        new("e8751fda-24b9-4727-ad66-f81de722f64f"),
        new("ccfa3a94-c7ea-4011-8b0f-b3364f4c023a"),
        new("68084dee-fa5c-4669-b3c0-d64e9aca182b"),
        new("a4254427-84f7-4ba3-9c8a-2aea8862fde6"),
        new("5719d04d-3093-4293-87d9-17f5bd9d9a7e"),
        new("e732f5f9-db94-405b-9221-f4449b4baad7"),
        new("e768769e-3a89-425d-9f99-3610e8e43bb9"),
        new("c78b3a6c-5517-4c56-ad1d-b0da8bfc37c3"),
        new("a3a4afd8-17e1-4d9f-8da5-5883331c360f"),
        new("b24068e1-bd66-4d79-a1c6-aa6a79f50edc"),
        new("1aed82ba-f96f-453b-b2b0-7d30498659cb"),
        new("f18b4488-39e9-406c-b632-5e635c9972bb"),
        new("e3bd88b6-54b6-43ec-9c94-ee0e36218618"),
        new("b59c6585-0c85-4c68-bb43-1f37e4aade22"),
        new("6f59e771-5dc0-44aa-9b7d-a84c3d0c7d74"),
        new("3d5f630e-66c3-43da-b73c-50d5be1792c3"),
        new("237bc85d-769a-468b-a048-70e3b5c382ee"),
    };

    [Fact]
    public void PublicComponentsConstructWithUniqueStableGuids()
    {
        Assembly assembly = LoadPlugin();
        List<GH_Component> components = ComponentTypes(assembly)
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .ToList();

        Assert.All(components, component => Assert.Equal("InvisibleDragon", component.Category));
        Assert.Equal(components.Count, components.Select(component => component.ComponentGuid).Distinct().Count());
        Assert.DoesNotContain(new Guid("c25eb6d8-9500-44e5-9909-58d41de0a320"), components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("1938b273-3a60-459b-beb2-92e7c4905053"), components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("d1930bb6-4398-46b9-a661-451370f09103"), components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("20a8a2f5-845e-4a46-aa03-fb8849f592e2"), components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("4f443564-2e13-4a79-8845-27d1e6eb285d"), components.Select(component => component.ComponentGuid));
        Assert.All(HvacComponentGuids, guid => Assert.Contains(guid, components.Select(component => component.ComponentGuid)));
    }

    [Fact]
    public void PluginAssemblyUsesGrasshopperExtensionAndAllComponentsLoad()
    {
        Assembly assembly = LoadPlugin();
        Type[] componentTypes = ComponentTypes(assembly);

        Assert.EndsWith(".gha", assembly.Location, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(36, componentTypes.Length);
        Assert.All(componentTypes, type => Assert.NotNull(Activator.CreateInstance(type)));
    }

    [Fact]
    public void PublicComponentsDoNotExposeEntityIdentifiersOrRelationshipIndices()
    {
        Assembly assembly = LoadPlugin();
        GH_Component[] components = ComponentTypes(assembly)
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .ToArray();

        Assert.DoesNotContain(
            components.SelectMany(component => component.Params.Input),
            parameter => string.Equals(parameter.Name, "ID", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.EndsWith(" ID", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parameter.NickName, "ID", StringComparison.OrdinalIgnoreCase));
        Assert.All(
            components.SelectMany(component => component.Params.Input),
            parameter =>
            {
                Assert.DoesNotContain("Index", parameter.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Indices", parameter.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Index", parameter.Description, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Indices", parameter.Description, StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public void EveryComponentHasItsOwnEmbeddedTwentyFourPixelIcon()
    {
        const string prefix =
            "Dragons.InvisibleDragon.Grasshopper.Resources.Components.";
        Assembly assembly = LoadPlugin();
        Type[] componentTypes = ComponentTypes(assembly);
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(componentTypes.Length, resources.Length);
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (Type type in componentTypes)
        {
            string resourceName = prefix + type.Name + ".png";
            Assert.Contains(resourceName, resources);
            using Stream stream = Assert.IsAssignableFrom<Stream>(
                assembly.GetManifestResourceStream(resourceName));
            using var bitmap = new Bitmap(stream);
            Assert.Equal(24, bitmap.Width);
            Assert.Equal(24, bitmap.Height);
            AssertTransparentBorder(bitmap);

            stream.Position = 0;
            using SHA256 sha = SHA256.Create();
            Assert.True(hashes.Add(Convert.ToHexString(sha.ComputeHash(stream))), resourceName);

            GH_Component component = Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
            Bitmap? icon = component.Icon_24x24;
            Assert.NotNull(icon);
            Assert.Equal(24, icon.Width);
            Assert.Equal(24, icon.Height);
        }
    }

    [Fact]
    public void EveryParameterHasItsOwnEmbeddedTwentyFourPixelIcon()
    {
        const string prefix =
            "Dragons.InvisibleDragon.Grasshopper.Resources.Parameters.";
        Assembly assembly = typeof(DragonMaterialParam).Assembly;
        Type[] parameterTypes = ParameterTypes(assembly);
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(19, parameterTypes.Length);
        Assert.Equal(parameterTypes.Length, resources.Length);
        var resourceHashes = new HashSet<string>(StringComparer.Ordinal);
        var runtimeHashes = new HashSet<string>(StringComparer.Ordinal);
        Bitmap? defaultIcon = new NullIconStringParam().Icon_24x24;
        Assert.NotNull(defaultIcon);
        string defaultHash = PixelHash(defaultIcon);

        foreach (Type type in parameterTypes)
        {
            string resourceName = prefix + type.Name + ".png";
            Assert.Contains(resourceName, resources);
            using Stream stream = Assert.IsAssignableFrom<Stream>(
                assembly.GetManifestResourceStream(resourceName));
            using var bitmap = new Bitmap(stream);
            Assert.Equal(24, bitmap.Width);
            Assert.Equal(24, bitmap.Height);
            AssertTransparentBorder(bitmap);

            stream.Position = 0;
            using SHA256 sha = SHA256.Create();
            Assert.True(
                resourceHashes.Add(Convert.ToHexString(sha.ComputeHash(stream))),
                resourceName);

            GH_DocumentObject parameter = Assert.IsAssignableFrom<GH_DocumentObject>(
                Activator.CreateInstance(type));
            Bitmap? icon = parameter.Icon_24x24;
            Assert.NotNull(icon);
            Assert.Equal(24, icon.Width);
            Assert.Equal(24, icon.Height);
            string runtimeHash = PixelHash(icon);
            Assert.NotEqual(defaultHash, runtimeHash);
            Assert.True(runtimeHashes.Add(runtimeHash), type.FullName);
        }
    }

    private static void AssertTransparentBorder(Bitmap bitmap)
    {
        for (int pixel = 0; pixel < 24; pixel++)
        {
            foreach (int edge in new[] { 0, 1, 22, 23 })
            {
                Assert.Equal(0, bitmap.GetPixel(edge, pixel).A);
                Assert.Equal(0, bitmap.GetPixel(pixel, edge).A);
            }
        }
    }

    private static Type[] ComponentTypes(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(GH_Component).IsAssignableFrom(type))
            .ToArray();
    }

    private static Type[] ParameterTypes(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(type => type.IsPublic
                && !type.IsAbstract
                && typeof(IGH_Param).IsAssignableFrom(type)
                && type.Namespace == "Dragons.InvisibleDragon.Grasshopper.Parameters")
            .ToArray();
    }

    private static string PixelHash(Bitmap bitmap)
    {
        byte[] bytes = new byte[bitmap.Width * bitmap.Height * sizeof(int)];
        int offset = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                BitConverter.GetBytes(bitmap.GetPixel(x, y).ToArgb()).CopyTo(bytes, offset);
                offset += sizeof(int);
            }
        }

        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static Assembly LoadPlugin()
    {
        string repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        string path = Path.Combine(
            repositoryRoot,
            "temp",
            "build",
            "bin",
            "Dragons.InvisibleDragon.GH",
            "Release",
            "net8.0-windows",
            "Dragons.InvisibleDragon.GH.gha");
        Assert.True(File.Exists(path), $"Expected built Grasshopper assembly at '{path}'.");
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

    private sealed class NullIconStringParam : GH_PersistentParam<GH_String>
    {
        internal NullIconStringParam()
            : base("Default Icon Probe", "Probe", "Default icon probe.", "Tests", "Tests")
        {
        }

        public override Guid ComponentGuid => new("bd247b2e-85e1-4e32-a63f-cc6d33081d43");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap? Icon => null;

        protected override GH_GetterResult Prompt_Singular(ref GH_String value)
        {
            return GH_GetterResult.cancel;
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GH_String> values)
        {
            return GH_GetterResult.cancel;
        }
    }
}
