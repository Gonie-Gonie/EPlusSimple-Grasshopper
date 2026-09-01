using System.Text;
using System.Text.Json;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Results;

/// <summary>
/// Deterministic System.Text.Json serialization for portable EnergyPlus result snapshots.
/// </summary>
public static class EnergyPlusResultJson
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    public static string Serialize(EnergyPlusSimulationResult result, bool writeIndented = false)
    {
        Guard.NotNull(result, nameof(result));
        return JsonSerializer.Serialize(result, BuildingEnergyJson.CreateOptions(writeIndented));
    }

    public static EnergyPlusSimulationResult Deserialize(string json)
    {
        Guard.NotNull(json, nameof(json));
        return JsonSerializer.Deserialize<EnergyPlusSimulationResult>(
                json,
                BuildingEnergyJson.CreateOptions())
            ?? throw new JsonException("The EnergyPlus result JSON contains no document.");
    }

    public static void WriteFile(
        string path,
        EnergyPlusSimulationResult result,
        bool writeIndented = true)
    {
        Guard.NotNull(path, nameof(path));
        File.WriteAllText(path, Serialize(result, writeIndented), Utf8WithoutBom);
    }

    public static EnergyPlusSimulationResult ReadFile(string path)
    {
        Guard.NotNull(path, nameof(path));
        return Deserialize(File.ReadAllText(path, Utf8WithoutBom));
    }
}
