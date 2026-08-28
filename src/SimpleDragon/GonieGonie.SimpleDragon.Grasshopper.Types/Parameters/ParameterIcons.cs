using System.Collections.Concurrent;

namespace GonieGonie.SimpleDragon.Grasshopper.Parameters;

internal static class ParameterIcons
{
    private const string ResourcePrefix =
        "GonieGonie.SimpleDragon.Grasshopper.Resources.Parameters.";

    private static readonly ConcurrentDictionary<string, Lazy<Bitmap>> IconValues =
        new(StringComparer.Ordinal);

    internal static Bitmap ForParameter(Type parameterType)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(parameterType);
#else
        if (parameterType is null)
        {
            throw new ArgumentNullException(nameof(parameterType));
        }
#endif

        string resourceName = ResourcePrefix + parameterType.Name + ".png";
        return IconValues.GetOrAdd(
            resourceName,
            name => new Lazy<Bitmap>(() => LoadBitmap(name), true)).Value;
    }

    private static Bitmap LoadBitmap(string resourceName)
    {
        using Stream stream = typeof(ParameterIcons).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                "Missing embedded SimpleDragon parameter icon '" + resourceName + "'.");
        using var resourceBitmap = new Bitmap(stream);
        return new Bitmap(resourceBitmap);
    }
}
