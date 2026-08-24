using System.Text.Json;
using System.Text.Json.Serialization;

namespace GonieGonie.BuildingEnergy.Contracts;

/// <summary>
/// Creates invariant JSON options shared by model, manifest, and Grasshopper serializers.
/// </summary>
public static class BuildingEnergyJson
{
    /// <summary>
    /// Creates an independent options instance with stable contract defaults.
    /// </summary>
    /// <param name="writeIndented">Whether output should include stable indentation.</param>
    public static JsonSerializerOptions CreateOptions(bool writeIndented = false)
    {
        JsonSerializerOptions options = new()
        {
            AllowTrailingCommas = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            NumberHandling = JsonNumberHandling.Strict,
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = SnakeCaseLowerNamingPolicy.Instance,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            WriteIndented = writeIndented,
        };

        options.Converters.Add(
            new JsonStringEnumConverter(
                SnakeCaseLowerNamingPolicy.Instance,
                allowIntegerValues: false));
        options.Converters.Add(new OrderedMapJsonConverterFactory());
        return options;
    }
}
