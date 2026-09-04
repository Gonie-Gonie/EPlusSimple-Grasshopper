using System.Collections.ObjectModel;
using GH_IO.Serialization;

namespace Dragons.SimpleDragon.Grasshopper.Types;

/// <summary>
/// Carries Rhino-independent source provenance alongside a typed GRM or GRR
/// inside Grasshopper without adding it to either interchange-file schema.
/// </summary>
public abstract class SimpleDragonGeometryContextGoo<T> : SimpleDragonGoo<T>
    where T : class
{
    private const string GeometryMapSnapshotKey = "GeometryMapSnapshot";

    protected SimpleDragonGeometryContextGoo()
    {
        GeometryMap = Array.Empty<GreenRetrofitGeometryMapEntry>();
    }

    protected SimpleDragonGeometryContextGoo(
        T value,
        IEnumerable<GreenRetrofitGeometryMapEntry>? geometryMap)
        : base(value)
    {
        GeometryMap = CopyGeometryMap(geometryMap);
    }

    /// <summary>
    /// Gets source-geometry provenance used automatically by CSV export.
    /// </summary>
    public IReadOnlyList<GreenRetrofitGeometryMapEntry> GeometryMap { get; private set; }

    public override bool CastFrom(object source)
    {
        if (source is SimpleDragonGeometryContextGoo<T> goo && goo.Value is not null)
        {
            Value = goo.Value;
            GeometryMap = CopyGeometryMap(goo.GeometryMap);
            return true;
        }

        bool cast = base.CastFrom(source);
        if (cast)
        {
            GeometryMap = Array.Empty<GreenRetrofitGeometryMapEntry>();
        }

        return cast;
    }

    public override bool Write(GH_IWriter writer)
    {
        if (!base.Write(writer))
        {
            return false;
        }

        if (Value is not null && GeometryMap.Count > 0)
        {
            writer.SetString(
                GeometryMapSnapshotKey,
                SimpleDragonGooSnapshot.SerializeGeometryMap(GeometryMap));
        }

        return true;
    }

    public override bool Read(GH_IReader reader)
    {
        if (!base.Read(reader))
        {
            return false;
        }

        GeometryMap = Value is not null && reader.ItemExists(GeometryMapSnapshotKey)
            ? SimpleDragonGooSnapshot.DeserializeGeometryMap(reader.GetString(GeometryMapSnapshotKey))
            : Array.Empty<GreenRetrofitGeometryMapEntry>();
        return true;
    }

    private static ReadOnlyCollection<GreenRetrofitGeometryMapEntry> CopyGeometryMap(
        IEnumerable<GreenRetrofitGeometryMapEntry>? geometryMap)
    {
        GreenRetrofitGeometryMapEntry[] copy = geometryMap?.ToArray()
            ?? Array.Empty<GreenRetrofitGeometryMapEntry>();
        if (copy.Any(item => item is null))
        {
            throw new ArgumentException("A geometry-map item cannot be null.", nameof(geometryMap));
        }

        return Array.AsReadOnly(copy);
    }
}
