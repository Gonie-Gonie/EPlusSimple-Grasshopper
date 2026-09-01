using Dragons.SimpleDragon.Batch;

namespace Dragons.SimpleDragon.Grasshopper.Types;

/// <summary>
/// Immutable Grasshopper authoring value that owns one GRM alternative and its optional stable case ID.
/// Runtime and weather locations remain module-managed and are deliberately not part of this value.
/// </summary>
public sealed class SimpleDragonBatchCase
{
    public SimpleDragonBatchCase(GreenRetrofitModel model, string? caseId = null)
    {
        var validated = new BatchCaseDefinition(
            model ?? throw new ArgumentNullException(nameof(model)),
            string.IsNullOrWhiteSpace(caseId) ? null : caseId);
        Model = AuthoringValueSupport.Copy(validated.Model);
        CaseId = validated.CaseId;
    }

    public GreenRetrofitModel Model { get; }

    public string? CaseId { get; }
}

/// <summary>
/// Grasshopper wrapper for one directly composed SimpleDragon batch case.
/// </summary>
public sealed class SimpleDragonBatchCaseGoo : SimpleDragonGoo<SimpleDragonBatchCase>
{
    public SimpleDragonBatchCaseGoo()
    {
    }

    public SimpleDragonBatchCaseGoo(SimpleDragonBatchCase value)
        : base(value)
    {
    }

    public override string TypeName => "SimpleDragon Batch Case";

    public override string TypeDescription =>
        "One SimpleDragon GRM alternative with its optional stable batch case ID.";

    protected override SimpleDragonGoo<SimpleDragonBatchCase> Create(SimpleDragonBatchCase value) =>
        new SimpleDragonBatchCaseGoo(value);

    protected override SimpleDragonGoo<SimpleDragonBatchCase> CreateEmpty() =>
        new SimpleDragonBatchCaseGoo();

    protected override string DisplayText(SimpleDragonBatchCase value) =>
        value.CaseId is null
            ? $"Batch Case {value.Model.Name} (derived ID)"
            : $"Batch Case {value.CaseId}: {value.Model.Name}";
}
