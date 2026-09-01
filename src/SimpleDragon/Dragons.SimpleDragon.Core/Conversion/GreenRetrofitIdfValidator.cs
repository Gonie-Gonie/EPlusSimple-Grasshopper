using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Idf;

namespace Dragons.SimpleDragon;

/// <summary>
/// Validates a pinned SimpleDragon-compatible IDF without changing its emitted text.
/// </summary>
public static class GreenRetrofitIdfValidator
{
    private const string ActivityScheduleName = "$DEFAULT$PEOPLEACTIVITY";
    private const string LegacyRealTypeName = "Real";
    private const string DeclaredRealTypeName = "ScheduleTypeLimits:Real";

    /// <summary>
    /// Validates every IDD rule while preserving the upstream 0.7.0 people-activity
    /// schedule reference as a documented warning rather than a Grasshopper error.
    /// </summary>
    public static ValidationResult Validate(IdfDocument document)
    {
#if NET48
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }
#else
        ArgumentNullException.ThrowIfNull(document);
#endif

        bool hasDeclaredRealType = document["ScheduleTypeLimits"].Any(
            item => string.Equals(item.Name, DeclaredRealTypeName, StringComparison.OrdinalIgnoreCase));
        bool normalizedLegacyReference = false;
        var validationObjects = new List<IdfObject>(document.Count);
        foreach (IdfObject item in document)
        {
            string[] values = item.Fields.Select(field => field.Value).ToArray();
            if (hasDeclaredRealType
                && string.Equals(item.ObjectType, "Schedule:Constant", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Name, ActivityScheduleName, StringComparison.OrdinalIgnoreCase)
                && values.Length > 1
                && string.Equals(values[1], LegacyRealTypeName, StringComparison.OrdinalIgnoreCase))
            {
                values[1] = DeclaredRealTypeName;
                normalizedLegacyReference = true;
            }

            validationObjects.Add(new IdfObject(item.ObjectType, values));
        }

        ValidationResult validation = IdfValidator.Validate(
            new IdfDocument(document.Schema, validationObjects));
        if (!normalizedLegacyReference)
        {
            return validation;
        }

        return validation.Add(new Diagnostic(
            "SD.IDF.LEGACY_PEOPLE_ACTIVITY_TYPE_LIMIT",
            DiagnosticSeverity.Warning,
            "Pinned Python 0.7.0 compatibility keeps the $DEFAULT$PEOPLEACTIVITY type-limit reference as 'Real'; "
                + "EnergyPlus accepts it with a not-validated warning.",
            suggestedAction: "Use the native InvisibleDragon IDF mode when exact legacy authoring text is not required."));
    }
}
