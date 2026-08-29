using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Grasshopper.Types;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

/// <summary>
/// Builds path-free execution documents with the EnergyPlus 24.2 positions that
/// differ from the preserved Python-oracle layout. The compatibility exporter
/// remains unchanged; only the canonical execution handoff uses this schema.
/// </summary>
internal static class EnergyPlus242ExecutionIdf
{
    internal static GonieGonie.InvisibleDragon.Idd.IddSchema Schema =>
        EnergyPlus242ExecutionIdfBuilder.PositioningSchema;

    internal static IdfDocument Create(EnergyModel model, EnergyModelIdfOptions options)
    {
        return EnergyPlus242ExecutionIdfBuilder.Create(model, options);
    }
}
