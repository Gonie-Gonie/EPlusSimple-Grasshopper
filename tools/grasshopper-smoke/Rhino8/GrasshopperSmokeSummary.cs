using System.Text.Json;

namespace GonieGonie.Dragons.GrasshopperSmoke.Rhino8;

internal sealed record GrasshopperSmokeSummary(
    string Host,
    string RhinoVersion,
    string GrasshopperVersion,
    int RegisteredInvisibleComponents,
    int RegisteredInvisibleParameters,
    int RegisteredSimpleComponents,
    int RegisteredSimpleParameters,
    int ReopenedObjectCount,
    string InvisibleGooType,
    string InvisibleGooValueName,
    string SimpleGooType,
    string SimpleGooValueName,
    string DocumentPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public void Write(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public string ToConsoleText()
    {
        return $"Rhino 8 Grasshopper host gate passed: {RegisteredInvisibleComponents} InvisibleDragon components, " +
            $"{RegisteredInvisibleParameters} parameters, {RegisteredSimpleComponents} SimpleDragon components, " +
            $"{RegisteredSimpleParameters} parameters, {ReopenedObjectCount} reopened objects, " +
            $"SimpleDragon Goo '{SimpleGooValueName}'.";
    }
}
