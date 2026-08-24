using GonieGonie.BuildingEnergy.Contracts;

namespace GonieGonie.SimpleDragon;

/// <summary>
/// Metadata for the pinned upstream Green Retrofit Model JSON format.
/// </summary>
public static class GrmFormat
{
    public const string Version = "0.7.0";
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
