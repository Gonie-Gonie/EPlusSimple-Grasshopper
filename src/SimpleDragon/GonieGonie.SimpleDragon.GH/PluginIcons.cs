using System.Collections.Concurrent;

namespace GonieGonie.SimpleDragon.Grasshopper;

internal static class PluginIcons
{
    private const string Icon24ResourceName =
        "GonieGonie.SimpleDragon.Grasshopper.Resources.SimpleDragon24.png";
    private const string ComponentResourcePrefix =
        "GonieGonie.SimpleDragon.Grasshopper.Resources.Components.";

    private static readonly Lazy<Bitmap?> Icon24Value = new(() => LoadBitmap(Icon24ResourceName));
    private static readonly ConcurrentDictionary<string, Lazy<Bitmap?>> ComponentIconValues =
        new(StringComparer.Ordinal);

    internal static Bitmap? Icon24 => Icon24Value.Value;

    internal static Bitmap? ForComponent(Type componentType)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(componentType);
#else
        if (componentType is null)
        {
            throw new ArgumentNullException(nameof(componentType));
        }
#endif

        string resourceName = ComponentResourcePrefix + componentType.Name + ".png";
        Bitmap? componentIcon = ComponentIconValues.GetOrAdd(
            resourceName,
            name => new Lazy<Bitmap?>(() => LoadBitmap(name), true)).Value;
        return componentIcon ?? Icon24;
    }

    private static Bitmap? LoadBitmap(string resourceName)
    {
        using Stream? stream = typeof(PluginIcons).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var resourceBitmap = new Bitmap(stream);
        return new Bitmap(resourceBitmap);
    }
}
