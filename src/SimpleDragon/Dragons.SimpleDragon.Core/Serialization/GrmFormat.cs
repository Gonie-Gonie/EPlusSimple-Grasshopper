using System.Collections.ObjectModel;
using Dragons.BuildingEnergy.Contracts;

namespace Dragons.SimpleDragon;

/// <summary>
/// Metadata for the pinned upstream Green Retrofit Model JSON format.
/// </summary>
public static class GrmFormat
{
    public const string Version = "0.7.0";

    private static readonly int[] LegacyVintage = { 1900, 1, 1 };

    /// <summary>
    /// Creates a fresh, deeply immutable copy of the pinned upstream
    /// <c>GRJSON_FORMAT</c> input template.
    /// </summary>
    /// <remarks>
    /// This is the historical Excel-input bootstrap shape. It is intentionally
    /// separate from the canonical GRM writer, whose populated system groups
    /// are JSON objects rather than the empty arrays retained by this template.
    /// </remarks>
    public static OrderedMap<object> CreateLegacyInputTemplate()
    {
        var building = new OrderedMap<object>(new[]
        {
            Entry("name", string.Empty),
            Entry("north_axis", 0),
            Entry("address", string.Empty),
            Entry("vintage", Vintage()),
            Entry("num_aboveground_floors", 0),
            Entry("num_underground_floors", 0),
            Entry("floors", EmptyList()),
            Entry("supply_systems", EmptyList()),
            Entry("source_systems", EmptyList()),
            Entry("ventilation_systems", EmptyList()),
            Entry("photovoltaic_systems", EmptyList()),
        });

        return new OrderedMap<object>(new[]
        {
            Entry("building", building),
            Entry("materials", EmptyList()),
            Entry("surface_constructions", EmptyList()),
            Entry("fenestration_constructions", EmptyList()),
        });
    }

    private static KeyValuePair<string, object> Entry(string key, object value)
    {
        return new KeyValuePair<string, object>(key, value);
    }

    private static ReadOnlyCollection<int> Vintage()
    {
        return Array.AsReadOnly((int[])LegacyVintage.Clone());
    }

    private static ReadOnlyCollection<object> EmptyList()
    {
        return new List<object>().AsReadOnly();
    }
}

/// <summary>
/// A GRM read operation that can retain a partial model alongside diagnostics.
/// </summary>
public sealed class GrmReadResult
{
    internal GrmReadResult(GreenRetrofitModel? model, IReadOnlyList<Diagnostic> diagnostics)
    {
        Model = model;
        Diagnostics = diagnostics;
    }

    public GreenRetrofitModel? Model { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Success => Model is not null && Diagnostics.All(item => !item.IsFailure);

    public GreenRetrofitModel RequireModel()
    {
        if (Model is not null && Diagnostics.All(item => !item.IsFailure))
        {
            return Model;
        }

        string message = Diagnostics.Count == 0
            ? "The GRM document did not produce a model."
            : Diagnostics[0].Message;
        throw new InvalidDataException(message);
    }
}
