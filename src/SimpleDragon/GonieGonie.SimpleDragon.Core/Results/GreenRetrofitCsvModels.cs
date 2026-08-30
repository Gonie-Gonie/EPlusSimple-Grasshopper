using System.Collections.ObjectModel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

/// <summary>
/// Stable entity kinds used by the Rhino-independent geometry-map CSV schema.
/// </summary>
public enum GreenRetrofitGeometryKind
{
    Zone,
    Surface,
    Fenestration,
}

/// <summary>
/// Rhino-independent source mapping for one SimpleDragon domain entity.
/// </summary>
public sealed class GreenRetrofitGeometryMapEntry
{
    public GreenRetrofitGeometryMapEntry(
        EntityId entityId,
        GreenRetrofitGeometryKind kind,
        int zoneIndex,
        int? surfaceIndex,
        int? openingIndex,
        int? trimLoopIndex,
        GeometryProvenance provenance)
    {
        EntityId = DomainSupport.NotNull(entityId, nameof(entityId));
        if (!Enum.IsDefined(typeof(GreenRetrofitGeometryKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown geometry-map kind.");
        }

#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(zoneIndex);
#else
        if (zoneIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoneIndex));
        }
#endif

        if (surfaceIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(surfaceIndex));
        }

        if (openingIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(openingIndex));
        }

        if (trimLoopIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trimLoopIndex));
        }

        bool mapsZone = kind == GreenRetrofitGeometryKind.Zone;
        bool mapsFenestration = kind == GreenRetrofitGeometryKind.Fenestration;
        if (mapsZone == surfaceIndex.HasValue)
        {
            throw new ArgumentException(
                mapsZone
                    ? "A zone geometry mapping must not identify one owned surface."
                    : "A surface or fenestration geometry mapping requires a surface index.",
                nameof(surfaceIndex));
        }

        if (!mapsFenestration && (openingIndex.HasValue || trimLoopIndex.HasValue))
        {
            throw new ArgumentException(
                "Only a fenestration geometry mapping may identify an opening or trim loop.",
                nameof(openingIndex));
        }

        if (mapsFenestration && !openingIndex.HasValue)
        {
            throw new ArgumentException(
                "A fenestration geometry mapping requires an opening index.",
                nameof(openingIndex));
        }

        Kind = kind;
        ZoneIndex = zoneIndex;
        SurfaceIndex = surfaceIndex;
        OpeningIndex = openingIndex;
        TrimLoopIndex = trimLoopIndex;
        Provenance = DomainSupport.NotNull(provenance, nameof(provenance));
    }

    public EntityId EntityId { get; }

    public GreenRetrofitGeometryKind Kind { get; }

    public int ZoneIndex { get; }

    public int? SurfaceIndex { get; }

    public int? OpeningIndex { get; }

    public int? TrimLoopIndex { get; }

    public GeometryProvenance Provenance { get; }
}

/// <summary>
/// One deterministic CSV file in a SimpleDragon export package.
/// </summary>
public sealed class GreenRetrofitCsvFile
{
    internal GreenRetrofitCsvFile(string name, string content)
    {
        Name = name;
        Content = content;
    }

    public string Name { get; }

    /// <summary>
    /// Gets CSV text without a BOM. File export adds the BOM as bytes.
    /// </summary>
    public string Content { get; }
}

/// <summary>
/// Complete, ordered set of SimpleDragon result CSV files.
/// </summary>
public sealed class GreenRetrofitCsvPackage
{
    internal GreenRetrofitCsvPackage(IEnumerable<GreenRetrofitCsvFile> files)
    {
        Files = new ReadOnlyCollection<GreenRetrofitCsvFile>(files.ToArray());
    }

    public IReadOnlyList<GreenRetrofitCsvFile> Files { get; }

    public GreenRetrofitCsvFile GetFile(string name)
    {
        string required = DomainSupport.RequiredText(name, nameof(name));
        return Files.Single(file => string.Equals(file.Name, required, StringComparison.Ordinal));
    }
}

/// <summary>
/// Describes one triggered or preview-only CSV directory export.
/// </summary>
public sealed class GreenRetrofitCsvExportResult
{
    internal GreenRetrofitCsvExportResult(
        string directoryPath,
        bool exportRequested,
        bool written,
        IEnumerable<string> filePaths)
    {
        DirectoryPath = directoryPath;
        ExportRequested = exportRequested;
        Written = written;
        FilePaths = new ReadOnlyCollection<string>(filePaths.ToArray());
    }

    public string DirectoryPath { get; }

    public bool ExportRequested { get; }

    public bool Written { get; }

    public IReadOnlyList<string> FilePaths { get; }
}
