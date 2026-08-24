namespace GonieGonie.SimpleDragon.Grasshopper;

internal static class PluginIcons
{
    private const string Icon24ResourceName =
        "GonieGonie.SimpleDragon.Grasshopper.Resources.SimpleDragon24.png";

    private static readonly Lazy<Bitmap?> Icon24Value = new(LoadIcon24);

    internal static Bitmap? Icon24 => Icon24Value.Value;

    private static Bitmap? LoadIcon24()
    {
        using Stream? stream = typeof(PluginIcons).Assembly.GetManifestResourceStream(Icon24ResourceName);
        if (stream is null)
        {
            return null;
        }

        using var resourceBitmap = new Bitmap(stream);
        return new Bitmap(resourceBitmap);
    }
}
