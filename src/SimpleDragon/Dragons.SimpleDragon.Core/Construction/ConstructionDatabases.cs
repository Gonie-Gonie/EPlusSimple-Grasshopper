using System.Collections.ObjectModel;
using System.Globalization;
using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon;

/// <summary>
/// Deterministically ordered material database loaded from the embedded upstream CSV.
/// </summary>
public sealed class MaterialDatabase
{
    private readonly ReadOnlyDictionary<string, Material> _byName;

    internal MaterialDatabase(CsvDocument document)
    {
        var items = new List<Material>(document.Rows.Count);
        var byName = new Dictionary<string, Material>(StringComparer.Ordinal);
        foreach (CsvRow row in document.Rows)
        {
            string name = row.Required("name");
            var material = new Material(
                name,
                row.Number("conductivity"),
                row.Number("density"),
                row.Number("heat_capacity"),
                DeterministicDomainId.Create("MTRL-DB", name));
            if (byName.ContainsKey(name))
            {
                throw row.Error("Duplicate material name '" + name + "'.");
            }

            byName.Add(name, material);
            items.Add(material);
        }

        Items = items.AsReadOnly();
        _byName = new ReadOnlyDictionary<string, Material>(byName);
    }

    public IReadOnlyList<Material> Items { get; }

    public LookupResult<Material> Find(string? name)
    {
        string key = name?.Trim() ?? string.Empty;
        if (key.Length > 0 && _byName.TryGetValue(key, out Material? material))
        {
            return LookupResults.Success(material);
        }

        return LookupResults.Failure<Material>(new Diagnostic(
            "SD.DB.MATERIAL_NOT_FOUND",
            DiagnosticSeverity.Error,
            key.Length == 0
                ? "A material name is required."
                : "Material '" + key + "' was not found in the embedded database.",
            suggestedAction: "Select one of MaterialDatabase.Items."));
    }
}

/// <summary>
/// Korean opaque-construction regulations and upstream-compatible selection rules.
/// </summary>
public sealed class SurfaceConstructionDatabase
{
    private readonly ReadOnlyDictionary<SurfaceRegulationKey, SurfaceConstruction> _byKey;

    internal SurfaceConstructionDatabase(CsvDocument document, MaterialDatabase materials)
    {
        Material insulation = materials.Find("insulation").Require();
        Material concrete = materials.Find("concrete").Require();
        var entries = new List<SurfaceConstructionEntry>(document.Rows.Count);
        var byKey = new Dictionary<SurfaceRegulationKey, SurfaceConstruction>();
        var dates = new SortedSet<DateTime>();

        foreach (CsvRow row in document.Rows)
        {
            string effectiveDateText = row.Required("시행일자");
            if (!DateTime.TryParseExact(
                    effectiveDateText,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime effectiveDate))
            {
                throw row.Error("시행일자 must use yyyyMMdd format.");
            }

            var key = new SurfaceRegulationKey(
                effectiveDate,
                row.Required("부위"),
                row.Required("외기조건"),
                row.Required("용도"),
                row.Required("지역"));
            double uValue = row.Number("열관류율");
            var construction = SurfaceConstruction.CreateSimple(
                key.ToString(),
                uValue,
                insulation,
                concrete,
                id: DeterministicDomainId.Create("CTSF-DB", key.ToString()));
            if (byKey.ContainsKey(key))
            {
                throw row.Error("Duplicate surface regulation key '" + key + "'.");
            }

            byKey.Add(key, construction);
            entries.Add(new SurfaceConstructionEntry(key, construction, uValue));
            dates.Add(effectiveDate.Date);
        }

        Entries = entries.AsReadOnly();
        RegulationDates = dates.ToArray();
        _byKey = new ReadOnlyDictionary<SurfaceRegulationKey, SurfaceConstruction>(byKey);
    }

    public IReadOnlyList<SurfaceConstructionEntry> Entries { get; }

    public IReadOnlyList<DateTime> RegulationDates { get; }

    public LookupResult<SurfaceConstruction> Find(SurfaceRegulationKey? key)
    {
        if (key is not null && _byKey.TryGetValue(key, out SurfaceConstruction? construction))
        {
            return LookupResults.Success(construction);
        }

        return LookupResults.Failure<SurfaceConstruction>(new Diagnostic(
            "SD.DB.SURFACE_CONSTRUCTION_NOT_FOUND",
            DiagnosticSeverity.Error,
            key is null
                ? "A surface regulation key is required."
                : "Surface construction '" + key + "' was not found in the embedded regulation database.",
            suggestedAction: "Check the vintage, part, exposure, use, and climate-region values."));
    }

    public LookupResult<SurfaceConstruction> FindRegulated(
        DateTime vintage,
        SurfaceType surfaceType,
        SurfaceBoundaryCondition boundaryCondition,
        string? climateRegion,
        bool isRadiantFloor = false,
        bool isMultifamilyHousing = false)
    {
        if (!Enum.IsDefined(typeof(SurfaceType), surfaceType))
        {
            throw new ArgumentOutOfRangeException(nameof(surfaceType), surfaceType, "Unknown surface type.");
        }

        if (!Enum.IsDefined(typeof(SurfaceBoundaryCondition), boundaryCondition))
        {
            throw new ArgumentOutOfRangeException(nameof(boundaryCondition), boundaryCondition, "Unknown boundary condition.");
        }

        DateTime regulationDate = DateTime.MinValue;
        for (int index = 0; index < RegulationDates.Count; index++)
        {
            DateTime candidate = RegulationDates[index];
            if (candidate <= vintage.Date)
            {
                regulationDate = candidate;
            }
            else
            {
                break;
            }
        }

        if (regulationDate == DateTime.MinValue)
        {
            return LookupResults.Failure<SurfaceConstruction>(new Diagnostic(
                "SD.DB.SURFACE_VINTAGE_NOT_COVERED",
                DiagnosticSeverity.Error,
                "No surface regulation is available on or before "
                + vintage.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".",
                suggestedAction: "Use a vintage on or after the earliest regulation date."));
        }

        string climate = climateRegion?.Trim() ?? string.Empty;
        if (climate.Length == 0)
        {
            return LookupResults.Failure<SurfaceConstruction>(new Diagnostic(
                "SD.DB.CLIMATE_REGION_REQUIRED",
                DiagnosticSeverity.Error,
                "A Korean climate region is required for a surface regulation lookup.",
                suggestedAction: "Use WeatherDatabase.FindByAddress to resolve the climate region."));
        }

        string part = ResolvePart(surfaceType, boundaryCondition, isRadiantFloor);
        string use = isMultifamilyHousing ? "공동주택" : "공동주택 외";
        return Find(new SurfaceRegulationKey(regulationDate, part, "외기 직접", use, climate));
    }

    private static string ResolvePart(
        SurfaceType surfaceType,
        SurfaceBoundaryCondition boundaryCondition,
        bool isRadiantFloor)
    {
        if (surfaceType == SurfaceType.Wall)
        {
            return "외벽";
        }

        if (surfaceType == SurfaceType.Ceiling && boundaryCondition == SurfaceBoundaryCondition.Outdoors)
        {
            return "최상층 지붕";
        }

        if (surfaceType == SurfaceType.Floor
            && (boundaryCondition == SurfaceBoundaryCondition.Outdoors
                || boundaryCondition == SurfaceBoundaryCondition.Ground))
        {
            return isRadiantFloor ? "바닥난방인 최하층 바닥" : "바닥난방이 아닌 최하층 바닥";
        }

        return isRadiantFloor ? "바닥난방인 층간바닥" : "바닥난방이 아닌 층간바닥";
    }
}

public sealed class SurfaceConstructionEntry
{
    internal SurfaceConstructionEntry(
        SurfaceRegulationKey key,
        SurfaceConstruction construction,
        double regulatedUValue)
    {
        Key = key;
        Construction = construction;
        RegulatedUValue = regulatedUValue;
    }

    public SurfaceRegulationKey Key { get; }

    public SurfaceConstruction Construction { get; }

    public double RegulatedUValue { get; }
}

/// <summary>
/// Deterministically ordered Korean fenestration database.
/// </summary>
public sealed class FenestrationConstructionDatabase
{
    private readonly ReadOnlyDictionary<FenestrationConstructionKey, FenestrationConstruction> _byKey;

    internal FenestrationConstructionDatabase(CsvDocument document)
    {
        var entries = new List<FenestrationConstructionEntry>(document.Rows.Count);
        var byKey = new Dictionary<FenestrationConstructionKey, FenestrationConstruction>();
        foreach (CsvRow row in document.Rows)
        {
            var key = new FenestrationConstructionKey(
                row.Required("창개수"),
                row.Required("로이유리"),
                row.Required("아르곤"),
                row.Required("열교차단재"),
                row.Required("창틀"),
                row.Required("공기층"));
            var construction = new FenestrationConstruction(
                key.ToString(),
                row.Number("열관류율"),
                row.Number("SHGC"),
                DeterministicDomainId.Create("CTFN-DB", key.ToString()));
            if (byKey.ContainsKey(key))
            {
                throw row.Error("Duplicate fenestration key '" + key + "'.");
            }

            byKey.Add(key, construction);
            entries.Add(new FenestrationConstructionEntry(key, construction));
        }

        Entries = entries.AsReadOnly();
        _byKey = new ReadOnlyDictionary<FenestrationConstructionKey, FenestrationConstruction>(byKey);
    }

    public IReadOnlyList<FenestrationConstructionEntry> Entries { get; }

    public LookupResult<FenestrationConstruction> Find(FenestrationConstructionKey? key)
    {
        if (key is not null && _byKey.TryGetValue(key, out FenestrationConstruction? construction))
        {
            return LookupResults.Success(construction);
        }

        return LookupResults.Failure<FenestrationConstruction>(new Diagnostic(
            "SD.DB.FENESTRATION_CONSTRUCTION_NOT_FOUND",
            DiagnosticSeverity.Error,
            key is null
                ? "A fenestration key is required."
                : "Fenestration construction '" + key + "' was not found in the embedded regulation database.",
            suggestedAction: "Select values present in FenestrationConstructionDatabase.Entries."));
    }
}

public sealed class FenestrationConstructionEntry
{
    internal FenestrationConstructionEntry(
        FenestrationConstructionKey key,
        FenestrationConstruction construction)
    {
        Key = key;
        Construction = construction;
    }

    public FenestrationConstructionKey Key { get; }

    public FenestrationConstruction Construction { get; }
}
