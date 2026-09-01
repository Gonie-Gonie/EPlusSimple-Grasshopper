using System.Collections.ObjectModel;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Model;

/// <summary>
/// Deterministic tabular outputs used by the upstream monthly reporting workflow.
/// </summary>
public sealed class OutputTableSettings
{
    private static readonly string[] DefaultReports =
    {
        "EndUseEnergyConsumptionElectricityMonthly",
        "EndUseEnergyConsumptionNaturalGasMonthly",
        "EndUseEnergyConsumptionDieselMonthly",
        "EndUseEnergyConsumptionFuelOilMonthly",
        "EndUseEnergyConsumptionCoalMonthly",
        "EndUseEnergyConsumptionPropaneMonthly",
        "EndUseEnergyConsumptionGasolineMonthly",
        "EndUseEnergyConsumptionOtherFuelsMonthly",
    };

    public OutputTableSettings(IEnumerable<string>? summaryReports = null, bool includeElectricityBalanceMonthly = true)
    {
        string[] reports = (summaryReports ?? DefaultReports)
            .Select(report => DomainGuard.RequiredText(report, nameof(summaryReports)))
            .ToArray();
        if (reports.Distinct(StringComparer.OrdinalIgnoreCase).Count() != reports.Length)
        {
            throw new ArgumentException("Summary report names must be unique.", nameof(summaryReports));
        }

        SummaryReports = new ReadOnlyCollection<string>(reports);
        IncludeElectricityBalanceMonthly = includeElectricityBalanceMonthly;
    }

    public static OutputTableSettings Default { get; } = new();

    public IReadOnlyList<string> SummaryReports { get; }

    public bool IncludeElectricityBalanceMonthly { get; }

    internal IReadOnlyList<IdfObject> ToIdfObjects(IdfGenerationContext context)
    {
        List<IdfObject> objects = new()
        {
            context.CreateRaw("OutputControl:Table:Style", "Comma", "JtoKWH"),
            context.CreateRaw("Output:Table:SummaryReports", SummaryReports.Cast<object?>().ToArray()),
        };
        if (IncludeElectricityBalanceMonthly)
        {
            objects.Add(context.CreateRaw(
                "Output:Table:Monthly",
                "ElectricityBalanceMonthly",
                3,
                "ElectricityProduced:Facility",
                "SumOrAverage",
                "ElectricitySurplusSold:Facility",
                "SumOrAverage",
                "ElectricityPurchased:Facility",
                "SumOrAverage"));
        }

        return objects;
    }
}
