using System.Collections.Concurrent;

namespace GonieGonie.SimpleDragon.Grasshopper.Parameters;

internal static class ParameterIcons
{
    private const string ResourcePrefix =
        "GonieGonie.SimpleDragon.Grasshopper.Resources.Parameters.";

    private static readonly ConcurrentDictionary<string, Lazy<Bitmap>> IconValues =
        new(StringComparer.Ordinal);

    private static readonly HashSet<string> FallbackResourceNames = new(StringComparer.Ordinal)
    {
        ResourcePrefix + nameof(SimpleDragonSurfaceConstructionLayerParam) + ".png",
    };

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
        using Stream? stream = typeof(ParameterIcons).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            if (!FallbackResourceNames.Contains(resourceName))
            {
                throw new InvalidOperationException(
                    "Missing embedded SimpleDragon parameter icon '" + resourceName + "'.");
            }

            return CreateFallbackBitmap(resourceName);
        }

        using var resourceBitmap = new Bitmap(stream);
        return new Bitmap(resourceBitmap);
    }

    private static Bitmap CreateFallbackBitmap(string resourceName)
    {
        uint hash = 2166136261;
        foreach (char character in resourceName)
        {
            hash ^= character;
            hash *= 16777619;
        }

        var bitmap = new Bitmap(24, 24, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int red = 72 + (int)(hash & 0x7f);
        int green = 72 + (int)((hash >> 8) & 0x7f);
        int blue = 72 + (int)((hash >> 16) & 0x7f);
        using var fill = new SolidBrush(Color.FromArgb(235, red, green, blue));
        using var edge = new Pen(Color.FromArgb(245, 25, 20, 55), 1.6f);
        Point[] body =
        {
            new(12, 2),
            new(21, 7),
            new(21, 17),
            new(12, 22),
            new(3, 17),
            new(3, 7),
        };
        graphics.FillPolygon(fill, body);
        graphics.DrawPolygon(edge, body);

        using var mark = new Pen(Color.FromArgb(245, 255, 255, 255), 1.8f);
        for (int index = 0; index < 5; index++)
        {
            int y = 6 + index * 3;
            if (((hash >> (20 + index)) & 1) == 0)
            {
                graphics.DrawLine(mark, 7, y, 17, y);
            }
            else
            {
                graphics.DrawEllipse(mark, 10, y - 2, 4, 4);
            }
        }

        return bitmap;
    }
}
