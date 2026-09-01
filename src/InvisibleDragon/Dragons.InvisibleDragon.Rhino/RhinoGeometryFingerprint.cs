using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dragons.InvisibleDragon.Shape;

namespace Dragons.InvisibleDragon.Rhino;

/// <summary>
/// Produces stable, unit-normalized geometry fingerprints for provenance records.
/// </summary>
public static class RhinoGeometryFingerprint
{
    public static string ForPolygon(
        PlanarPolygon polygon,
        double toleranceMetres = GeometryTolerance.Distance)
    {
        if (polygon is null)
        {
            throw new ArgumentNullException(nameof(polygon));
        }

        ValidateTolerance(toleranceMetres);
        string canonical = CanonicalLoop(polygon.Vertices, toleranceMetres);
        return Hash(canonical);
    }

    public static string ForFace(
        PlanarPolygon outerLoop,
        IEnumerable<PlanarPolygon>? innerLoops,
        double toleranceMetres = GeometryTolerance.Distance)
    {
        if (outerLoop is null)
        {
            throw new ArgumentNullException(nameof(outerLoop));
        }

        ValidateTolerance(toleranceMetres);
        string outer = CanonicalLoop(outerLoop.Vertices, toleranceMetres);
        string[] holes = innerLoops is null
            ? Array.Empty<string>()
            : innerLoops
                .Select(loop => loop ?? throw new ArgumentException("A face cannot contain a null inner loop.", nameof(innerLoops)))
                .Select(loop => CanonicalLoop(loop.Vertices, toleranceMetres))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        return Hash(outer + "|" + string.Join("|", holes));
    }

    private static string CanonicalLoop(IReadOnlyList<Vertex> vertices, double tolerance)
    {
        string[] points = vertices.Select(vertex => Quantize(vertex, tolerance)).ToArray();
        var candidates = new List<string>(points.Length * 2);
        for (int start = 0; start < points.Length; start++)
        {
            candidates.Add(JoinRotation(points, start, reverse: false));
            candidates.Add(JoinRotation(points, start, reverse: true));
        }

        return candidates.OrderBy(value => value, StringComparer.Ordinal).First();
    }

    private static string JoinRotation(IReadOnlyList<string> points, int start, bool reverse)
    {
        var ordered = new string[points.Count];
        for (int offset = 0; offset < points.Count; offset++)
        {
            int index = reverse
                ? (start - offset + points.Count) % points.Count
                : (start + offset) % points.Count;
            ordered[offset] = points[index];
        }

        return string.Join(";", ordered);
    }

    private static string Quantize(Vertex vertex, double tolerance)
    {
        return string.Join(
            ",",
            Quantize(vertex.X, tolerance).ToString(CultureInfo.InvariantCulture),
            Quantize(vertex.Y, tolerance).ToString(CultureInfo.InvariantCulture),
            Quantize(vertex.Z, tolerance).ToString(CultureInfo.InvariantCulture));
    }

    private static long Quantize(double value, double tolerance)
    {
        double scaled = value / tolerance;
        if (scaled < long.MinValue || scaled > long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The coordinate cannot be quantized at this tolerance.");
        }

        return checked((long)Math.Round(scaled, MidpointRounding.AwayFromZero));
    }

    private static string Hash(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
#if NET6_0_OR_GREATER
        byte[] digest = SHA256.HashData(bytes);
#else
        byte[] digest;
        using (SHA256 sha = SHA256.Create())
        {
            digest = sha.ComputeHash(bytes);
        }
#endif
        return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void ValidateTolerance(double tolerance)
    {
        if (tolerance <= 0 || double.IsNaN(tolerance) || double.IsInfinity(tolerance))
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "A finite positive tolerance is required.");
        }
    }
}
