using System.Diagnostics.CodeAnalysis;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Rhino;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

/// <summary>
/// Canonical path-free SimpleDragon to InvisibleDragon handoff. SimpleDragon owns
/// weather selection/preparation; the output handle exposes identity, not a path.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Grasshopper owns component lifetime; RemovedFromDocument cancels and the continuation disposes the source.")]
public sealed class PrepareSimpleDragonSimulationComponent : SimpleDragonComponent
{
    private readonly object _sync = new();
    private CancellationTokenSource? _cancellation;
    private Task<PreparedWeatherOutcome>? _task;
    private PreparedWeatherOutcome? _result;
    private string? _key;
    private bool _removed;
    private int _solutionScheduled;

    public PrepareSimpleDragonSimulationComponent()
        : base(
            "Prepare SimpleDragon Simulation",
            "SD to IDF",
            "Converts a GRM to InvisibleDragon IDF and internally prepares the address-selected packaged EPW. No EnergyPlus, IDD, or EPW path is required.",
            SimpleDragonPanels.Model)
    {
    }

    public override Guid ComponentGuid => new("ca666fd7-788c-4682-8b04-fad8c7252fe0");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(
            new GreenRetrofitModelParam(),
            "GRM",
            "GRM",
            "SimpleDragon model whose address selects the packaged weather artifact.",
            GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonEnergyModelParam(),
            "Energy Model",
            "M",
            "Converted InvisibleDragon energy model.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new DragonIdfParam(),
            "IDF",
            "IDF",
            "Deterministic EnergyPlus IDF document.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new PreparedWeatherFileParam(),
            "Weather",
            "EPW",
            "Verified packaged EPW handle. The local artifact path remains internal.",
            GH_ParamAccess.item);
        pManager.AddBooleanParameter(
            "Success",
            "OK",
            "True when conversion passed and the packaged weather artifact is ready.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new DiagnosticParam(),
            "Diagnostics",
            "D",
            "Conversion and weather diagnostics.",
            GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        GreenRetrofitModelGoo? modelGoo = null;
        if (!DA.GetData(0, ref modelGoo) || modelGoo?.Value is null)
        {
            return;
        }

        GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(modelGoo.Value);
        var diagnostics = conversion.Diagnostics.ToList();
        if (conversion.EnergyModel is not null)
        {
            IdfDocument idf = EnergyPlus242ExecutionIdf.Create(
                conversion.EnergyModel,
                CreateExecutionIdfOptions());
            DA.SetData(0, new DragonEnergyModelGoo(conversion.EnergyModel));
            DA.SetData(1, new DragonIdfGoo(idf));
        }

        PreparedWeatherFile? weather = null;
        bool preparing = false;
        if (conversion.Weather is null)
        {
            diagnostics.Add(new Diagnostic(
                "SD.GH.WEATHER_SELECTION_MISSING",
                DiagnosticSeverity.Error,
                "The GRM does not contain an address-selected weather record.",
                suggestedAction: "Use SimpleDragon Model with a supported Korean address and vintage."));
        }
        else
        {
            PreparedWeatherOutcome? outcome = ResolveWeather(conversion.Weather, out preparing);
            if (outcome is not null)
            {
                diagnostics.AddRange(outcome.Diagnostics);
                weather = outcome.Weather;
            }
        }

        if (preparing)
        {
            Message = "Preparing Weather";
            diagnostics.Add(new Diagnostic(
                "SD.GH.WEATHER_PREPARING",
                DiagnosticSeverity.Info,
                "The address-selected packaged EPW is being verified and prepared off the Rhino UI thread."));
        }
        else
        {
            Message = weather is null ? "Weather Unavailable" : "Ready";
        }

        if (weather is not null)
        {
            DA.SetData(2, new PreparedWeatherFileGoo(weather));
        }

        bool success = conversion.EnergyModel is not null
            && weather is not null
            && diagnostics.All(item => !item.IsFailure);
        DA.SetData(3, success);
        Report(diagnostics);
        DA.SetDataList(4, diagnostics.Select(item => new DiagnosticGoo(item)));
    }

    /// <summary>
    /// Retains the SimpleDragon conversion semantics that affect model meaning,
    /// while using the valid EnergyPlus 24.2 HVAC field layout for the path-free
    /// execution document. The canonical execution handoff always uses this layout.
    /// </summary>
    internal static EnergyModelIdfOptions CreateExecutionIdfOptions() =>
        new()
        {
            ThrowOnValidationErrors = false,
            UseLegacyRectangularFenestration = true,
            UseLegacySimpleDragonScheduleMetadata = true,
            UseLegacySimpleDragonDefaultObjectFields = true,
            UseLegacySimpleDragonUsedProfileScheduleSelection = true,
            UseLegacySimpleDragonHvacTopology = false,
            UseLegacySimpleDragonVentilation = true,
        };

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        lock (_sync)
        {
            _removed = false;
        }
    }

    public override void RemovedFromDocument(GH_Document document)
    {
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            _removed = true;
            cancellation = _cancellation;
            _cancellation = null;
            _task = null;
            _result = null;
            _key = null;
        }

        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        base.RemovedFromDocument(document);
    }

    private PreparedWeatherOutcome? ResolveWeather(WeatherSelection selection, out bool preparing)
    {
        string key = SimpleDragonWeatherPackManifest.Supported.PackId + "|" + selection.EpwFileName;
        lock (_sync)
        {
            if (!string.Equals(_key, key, StringComparison.Ordinal))
            {
                try
                {
                    _cancellation?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                _key = key;
                _result = null;
                _task = null;
                _cancellation = null;
            }

            if (_result is not null)
            {
                PreparedWeatherOutcome result = _result;
                if (!result.IsSuccess)
                {
                    _result = null;
                }

                preparing = false;
                return result;
            }

            if (_task is null)
            {
                var cancellation = new CancellationTokenSource();
                CancellationToken token = cancellation.Token;
                Task<PreparedWeatherOutcome> task = Task.Run(() => PrepareWeather(selection, token), token);
                _cancellation = cancellation;
                _task = task;
                _ = task.ContinueWith(
                    completed => CompleteWeather(completed, key, cancellation),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            preparing = true;
            return null;
        }
    }

    private static PreparedWeatherOutcome PrepareWeather(
        WeatherSelection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            SimpleDragonWeatherFileResolution resolution = new SimpleDragonWeatherPackResolver().Resolve(
                selection,
                cancellationToken: cancellationToken);
            if (!resolution.IsSuccess || resolution.FilePath is null)
            {
                return new PreparedWeatherOutcome(null, resolution.Diagnostics);
            }

            PreparedWeatherFile weather = PreparedWeatherFile.FromVerifiedArtifact(
                resolution.FilePath,
                "SimpleDragon",
                selection.EpwFileName);
            return new PreparedWeatherOutcome(weather, resolution.Diagnostics);
        }
        catch (OperationCanceledException)
        {
            return PreparedWeatherOutcome.Failed(
                "SD.WEATHER.EXTRACTION_CANCELLED",
                DiagnosticSeverity.Info,
                "SimpleDragon weather preparation was cancelled.");
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is IOException
            || exception is InvalidOperationException
            || exception is UnauthorizedAccessException)
        {
            return PreparedWeatherOutcome.Failed(
                "SD.WEATHER.EXTRACTION_FAILED",
                DiagnosticSeverity.Error,
                exception.Message);
        }
    }

    private void CompleteWeather(
        Task<PreparedWeatherOutcome> completed,
        string key,
        CancellationTokenSource cancellation)
    {
        PreparedWeatherOutcome result = completed.Status == TaskStatus.RanToCompletion
            ? completed.Result
            : PreparedWeatherOutcome.Failed(
                completed.IsCanceled
                    ? "SD.WEATHER.EXTRACTION_CANCELLED"
                    : "SD.WEATHER.EXTRACTION_FAILED",
                completed.IsCanceled ? DiagnosticSeverity.Info : DiagnosticSeverity.Error,
                completed.IsCanceled
                    ? "SimpleDragon weather preparation was cancelled."
                    : completed.Exception?.GetBaseException().Message
                        ?? "SimpleDragon weather preparation failed.");
        bool schedule;
        lock (_sync)
        {
            schedule = ReferenceEquals(_task, completed)
                && string.Equals(_key, key, StringComparison.Ordinal);
            if (schedule)
            {
                _task = null;
                _cancellation = null;
                _result = result;
            }
        }

        cancellation.Dispose();
        if (schedule)
        {
            ScheduleSolution();
        }
    }

    private void ScheduleSolution()
    {
        if (Interlocked.Exchange(ref _solutionScheduled, 1) != 0)
        {
            return;
        }

        RhinoApp.InvokeOnUiThread((Action)(() =>
        {
            Interlocked.Exchange(ref _solutionScheduled, 0);
            lock (_sync)
            {
                if (_removed)
                {
                    return;
                }
            }

            GH_Document? document = OnPingDocument();
            document?.ScheduleSolution(5, _ => ExpireSolution(false));
        }));
    }

    private sealed class PreparedWeatherOutcome
    {
        internal PreparedWeatherOutcome(
            PreparedWeatherFile? weather,
            IEnumerable<Diagnostic> diagnostics)
        {
            Weather = weather;
            Diagnostics = diagnostics.ToArray();
        }

        internal PreparedWeatherFile? Weather { get; }

        internal IReadOnlyList<Diagnostic> Diagnostics { get; }

        internal bool IsSuccess => Weather is not null
            && Diagnostics.All(item => !item.IsFailure);

        internal static PreparedWeatherOutcome Failed(
            string code,
            DiagnosticSeverity severity,
            string message)
        {
            return new PreparedWeatherOutcome(
                null,
                new[] { new Diagnostic(code, severity, message) });
        }
    }
}
