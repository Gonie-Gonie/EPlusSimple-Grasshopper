using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

public sealed class ExportGreenRetrofitCsvComponent : SimpleDragonComponent
{
    public ExportGreenRetrofitCsvComponent()
        : base(
            "Export SimpleDragon CSV",
            "Export CSV",
            "Builds the deterministic SimpleDragon CSV package and writes it only when Export is true.",
            SimpleDragonPanels.Results)
    {
    }

    public override Guid ComponentGuid => new("9fe8a410-ea95-4eb8-81ec-56c45cdd029c");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitResultParam(), "GRR", "GRR", "SimpleDragon result to export.", GH_ParamAccess.item);
        pManager.AddParameter(
            new GreenRetrofitModelParam(),
            "GRM",
            "GRM",
            "Optional source model metadata for manifest.json.",
            GH_ParamAccess.item);
        pManager.AddTextParameter("Directory", "D", "Requested export directory.", GH_ParamAccess.item);
        pManager.AddTextParameter("Case ID", "Case", "Stable case identifier written to every CSV row.", GH_ParamAccess.item, string.Empty);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "Diag", "Optional diagnostics to include.", GH_ParamAccess.list);
        pManager.AddGenericParameter(
            "Geometry Map Data",
            "Map",
            "Structured GreenRetrofitGeometryMapEntry values from Extract SimpleDragon Zones.",
            GH_ParamAccess.list);
        pManager.AddBooleanParameter(
            "Export",
            "E",
            "Explicit file-write trigger. False previews content without creating a directory or files.",
            GH_ParamAccess.item,
            false);
        pManager.AddBooleanParameter(
            "Overwrite",
            "O",
            "Explicitly allow replacement of existing package files.",
            GH_ParamAccess.item,
            false);
        pManager[1].Optional = true;
        pManager[4].Optional = true;
        pManager[5].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Full Directory", "D", "Resolved export directory.", GH_ParamAccess.item);
        pManager.AddTextParameter("File Names", "N", "Stable manifest/CSV file order.", GH_ParamAccess.list);
        pManager.AddTextParameter("File Paths", "P", "Resolved paths that were or would be written.", GH_ParamAccess.list);
        pManager.AddTextParameter("Content", "C", "Deterministic manifest/CSV content in File Names order.", GH_ParamAccess.list);
        pManager.AddBooleanParameter("Written", "OK", "True only when this solution wrote every package file.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        GreenRetrofitResultGoo? resultGoo = null;
        GreenRetrofitModelGoo? modelGoo = null;
        string directory = string.Empty;
        string caseId = string.Empty;
        var diagnosticGoos = new List<DiagnosticGoo>();
        var geometryWrappers = new List<GH_ObjectWrapper>();
        bool export = false;
        bool overwrite = false;
        if (!DA.GetData(0, ref resultGoo)
            || !DA.GetData(2, ref directory)
            || !DA.GetData(3, ref caseId)
            || !DA.GetData(6, ref export)
            || !DA.GetData(7, ref overwrite)
            || resultGoo?.Value is null)
        {
            return;
        }

        DA.GetData(1, ref modelGoo);
        DA.GetDataList(4, diagnosticGoos);
        DA.GetDataList(5, geometryWrappers);
        Diagnostic[] diagnostics = diagnosticGoos
            .Where(item => item?.Value is not null)
            .Select(item => item.Value!)
            .ToArray();
        GreenRetrofitGeometryMapEntry[] geometryMap = geometryWrappers
            .Select(wrapper => wrapper?.Value)
            .OfType<GreenRetrofitGeometryMapEntry>()
            .ToArray();
        if (geometryMap.Length != geometryWrappers.Count)
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "Geometry Map Data contains a value that is not a GreenRetrofitGeometryMapEntry.");
            return;
        }

        GreenRetrofitCsvPackage package = GreenRetrofitCsvExporter.CreatePackage(
            resultGoo.Value,
            diagnostics,
            geometryMap,
            caseId,
            modelGoo?.Value);
        string fullDirectory = ResolveDocumentPath(directory);
        GreenRetrofitCsvExportResult preview = GreenRetrofitCsvExporter.ExportDirectory(
            fullDirectory,
            resultGoo.Value,
            diagnostics,
            geometryMap,
            caseId,
            modelGoo?.Value,
            export: false,
            overwrite: false);
        bool written = false;
        if (export)
        {
            try
            {
                GreenRetrofitCsvExportResult result = GreenRetrofitCsvExporter.ExportDirectory(
                    fullDirectory,
                    resultGoo.Value,
                    diagnostics,
                    geometryMap,
                    caseId,
                    modelGoo?.Value,
                    export: true,
                    overwrite);
                written = result.Written;
            }
            catch (IOException exception)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, exception.Message);
            }
        }

        DA.SetData(0, preview.DirectoryPath);
        DA.SetDataList(1, package.Files.Select(file => file.Name));
        DA.SetDataList(2, preview.FilePaths);
        DA.SetDataList(3, package.Files.Select(file => file.Content));
        DA.SetData(4, written);
    }
}

public abstract class MonthlySimpleDragonComponent : SimpleDragonComponent
{
    protected MonthlySimpleDragonComponent(
        string name,
        string nickname,
        string description)
        : base(name, nickname, description, SimpleDragonPanels.Results)
    {
    }

    protected static void RegisterDataInputs(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitResultParam(), "GRR", "GRR", "SimpleDragon result.", GH_ParamAccess.item);
        pManager.AddTextParameter("Metric", "M", "SiteUses, SourceUses, Carbon, or Cost.", GH_ParamAccess.item, "SiteUses");
        pManager.AddBooleanParameter("Gross", "G", "False for per-area values; true for gross values.", GH_ParamAccess.item, false);
        pManager.AddTextParameter("Grouping", "By", "Fuel or EndUse.", GH_ParamAccess.item, "Fuel");
    }

    protected static void RegisterPlotInputs(GH_InputParamManager pManager)
    {
        pManager.AddPlaneParameter("Plane", "P", "Plot plane.", GH_ParamAccess.item, Plane.WorldXY);
        pManager.AddNumberParameter("Width", "W", "Plot width in model units.", GH_ParamAccess.item, 12d);
        pManager.AddNumberParameter("Height", "H", "Plot height in model units.", GH_ParamAccess.item, 6d);
    }

    protected static void RegisterDataOutputs(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Series Names", "N", "Stable snake_case series names.", GH_ParamAccess.list);
        pManager.AddTextParameter("Month Names", "Months", "January through December.", GH_ParamAccess.list);
        pManager.AddNumberParameter("X Values", "X", "Month numbers, one branch per series.", GH_ParamAccess.tree);
        pManager.AddNumberParameter("Y Values", "Y", "Monthly values, one branch per series.", GH_ParamAccess.tree);
        pManager.AddTextParameter("Unit", "U", "Selected value unit.", GH_ParamAccess.item);
    }
}

public sealed class GreenRetrofitDataTreeComponent : MonthlySimpleDragonComponent
{
    public GreenRetrofitDataTreeComponent()
        : base(
            "SimpleDragon GRR Data Tree",
            "GRR Tree",
            "Outputs stable monthly fuel or end-use series as Grasshopper data trees.")
    {
    }

    public override Guid ComponentGuid => new("cb5a98f8-4188-4323-b55d-795b4a7ba20e");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        RegisterDataInputs(pManager);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        RegisterDataOutputs(pManager);
        pManager.AddTextParameter("CSV", "CSV", "Selected deterministic monthly CSV.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        if (!MonthlyComponentSupport.TryRead(DA, out MonthlyInput input, out string error))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
            return;
        }

        MonthlyComponentSupport.SetDataOutputs(DA, input.Data, 0);
        DA.SetData(
            5,
            GreenRetrofitCsvExporter.SerializeMonthly(input.Result, input.Data.Grouping));
    }
}

public sealed class GreenRetrofitMonthlyLinePlotComponent : MonthlySimpleDragonComponent
{
    public GreenRetrofitMonthlyLinePlotComponent()
        : base(
            "SimpleDragon Monthly Line Plot",
            "Monthly Lines",
            "Creates previewable monthly series curves and matching Grasshopper data trees.")
    {
    }

    public override Guid ComponentGuid => new("76e0c1b6-68d6-4cdc-a418-eea18aa131c1");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        RegisterDataInputs(pManager);
        RegisterPlotInputs(pManager);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddCurveParameter("Lines", "L", "One preview curve per series.", GH_ParamAccess.list);
        pManager.AddCurveParameter("Frame", "F", "Plot frame.", GH_ParamAccess.item);
        pManager.AddCurveParameter("Zero Axis", "Z", "Zero-value axis.", GH_ParamAccess.item);
        RegisterDataOutputs(pManager);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        if (!MonthlyComponentSupport.TryReadPlot(
                DA,
                out MonthlyInput input,
                out Plane plane,
                out double width,
                out double height,
                out string error))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
            return;
        }

        PlotScale scale = PlotGeometry.CreateScale(input.Data, height, stacked: false);
        Curve[] lines = input.Data.Series
            .Select(series => PlotGeometry.Line(series.Values, plane, width, scale))
            .ToArray();
        DA.SetDataList(0, lines);
        DA.SetData(1, PlotGeometry.Frame(plane, width, height));
        DA.SetData(2, PlotGeometry.ZeroAxis(plane, width, scale));
        MonthlyComponentSupport.SetDataOutputs(DA, input.Data, 3);
    }
}

public sealed class GreenRetrofitMonthlyBarPlotComponent : MonthlySimpleDragonComponent
{
    public GreenRetrofitMonthlyBarPlotComponent()
        : base(
            "SimpleDragon Monthly Bar Plot",
            "Monthly Bars",
            "Creates grouped or stacked monthly bar outlines and matching Grasshopper data trees.")
    {
    }

    public override Guid ComponentGuid => new("a73acba4-d98d-4fec-a846-dc982256d6b1");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        RegisterDataInputs(pManager);
        RegisterPlotInputs(pManager);
        pManager.AddBooleanParameter(
            "Stacked",
            "S",
            "True stacks series by month; false groups them side by side.",
            GH_ParamAccess.item,
            false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddCurveParameter("Bars", "B", "Bar-outline tree with one branch per series.", GH_ParamAccess.tree);
        pManager.AddCurveParameter("Frame", "F", "Plot frame.", GH_ParamAccess.item);
        pManager.AddCurveParameter("Zero Axis", "Z", "Zero-value axis.", GH_ParamAccess.item);
        RegisterDataOutputs(pManager);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        if (!MonthlyComponentSupport.TryReadPlot(
                DA,
                out MonthlyInput input,
                out Plane plane,
                out double width,
                out double height,
                out string error))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
            return;
        }

        bool stacked = false;
        if (!DA.GetData(7, ref stacked))
        {
            return;
        }

        PlotScale scale = PlotGeometry.CreateScale(input.Data, height, stacked);
        DA.SetDataTree(0, PlotGeometry.Bars(input.Data, plane, width, scale, stacked));
        DA.SetData(1, PlotGeometry.Frame(plane, width, height));
        DA.SetData(2, PlotGeometry.ZeroAxis(plane, width, scale));
        MonthlyComponentSupport.SetDataOutputs(DA, input.Data, 3);
    }
}

internal sealed class MonthlyInput
{
    internal MonthlyInput(GreenRetrofitResult result, GreenRetrofitMonthlyData data)
    {
        Result = result;
        Data = data;
    }

    internal GreenRetrofitResult Result { get; }

    internal GreenRetrofitMonthlyData Data { get; }
}

internal static class MonthlyComponentSupport
{
    internal static bool TryRead(
        IGH_DataAccess DA,
        out MonthlyInput input,
        out string error)
    {
        GreenRetrofitResultGoo? resultGoo = null;
        string metricText = "SiteUses";
        bool gross = false;
        string groupingText = "Fuel";
        if (!DA.GetData(0, ref resultGoo)
            || !DA.GetData(1, ref metricText)
            || !DA.GetData(2, ref gross)
            || !DA.GetData(3, ref groupingText)
            || resultGoo?.Value is null)
        {
            input = null!;
            error = "GRR, Metric, Gross, and Grouping inputs are required.";
            return false;
        }

        if (!Enum.TryParse(metricText.Trim(), true, out GreenRetrofitMetric metric))
        {
            input = null!;
            error = "Unknown GRR metric '" + metricText + "'.";
            return false;
        }

        string normalizedGrouping = new string(groupingText
            .Where(char.IsLetterOrDigit)
            .ToArray());
        if (!Enum.TryParse(normalizedGrouping, true, out GreenRetrofitSeriesGrouping grouping))
        {
            input = null!;
            error = "Unknown grouping '" + groupingText + "'. Use Fuel or EndUse.";
            return false;
        }

        input = new MonthlyInput(
            resultGoo.Value,
            GreenRetrofitMonthlyData.Create(resultGoo.Value, metric, gross, grouping));
        error = string.Empty;
        return true;
    }

    internal static bool TryReadPlot(
        IGH_DataAccess DA,
        out MonthlyInput input,
        out Plane plane,
        out double width,
        out double height,
        out string error)
    {
        plane = Plane.WorldXY;
        width = 12d;
        height = 6d;
        if (!TryRead(DA, out input, out error))
        {
            return false;
        }

        if (!DA.GetData(4, ref plane)
            || !DA.GetData(5, ref width)
            || !DA.GetData(6, ref height))
        {
            error = "Plane, Width, and Height inputs are required.";
            return false;
        }

        if (!plane.IsValid
            || double.IsNaN(width)
            || double.IsInfinity(width)
            || width <= 0d
            || double.IsNaN(height)
            || double.IsInfinity(height)
            || height <= 0d)
        {
            error = "Plot plane must be valid and plot dimensions must be finite and positive.";
            return false;
        }

        return true;
    }

    internal static void SetDataOutputs(
        IGH_DataAccess DA,
        GreenRetrofitMonthlyData data,
        int startIndex)
    {
        DA.SetDataList(startIndex, data.Series.Select(series => series.Name));
        DA.SetDataList(startIndex + 1, data.XLabels);
        DA.SetDataTree(startIndex + 2, NumberTree(data, useXValues: true));
        DA.SetDataTree(startIndex + 3, NumberTree(data, useXValues: false));
        DA.SetData(startIndex + 4, data.Unit);
    }

    private static GH_Structure<GH_Number> NumberTree(
        GreenRetrofitMonthlyData data,
        bool useXValues)
    {
        var tree = new GH_Structure<GH_Number>();
        for (int seriesIndex = 0; seriesIndex < data.Series.Count; seriesIndex++)
        {
            var path = new GH_Path(seriesIndex);
            IEnumerable<double> values = useXValues
                ? data.XValues.Select(value => (double)value)
                : data.Series[seriesIndex].Values;
            foreach (double value in values)
            {
                tree.Append(new GH_Number(value), path);
            }
        }

        return tree;
    }
}

internal readonly struct PlotScale
{
    internal PlotScale(double minimum, double maximum, double height)
    {
        Minimum = minimum;
        Maximum = maximum;
        Height = height;
    }

    internal double Minimum { get; }

    internal double Maximum { get; }

    internal double Height { get; }

    internal double Map(double value)
    {
        return Height * ((value - Minimum) / (Maximum - Minimum));
    }
}

internal static class PlotGeometry
{
    internal static PlotScale CreateScale(
        GreenRetrofitMonthlyData data,
        double height,
        bool stacked)
    {
        double minimum;
        double maximum;
        if (stacked)
        {
            minimum = 0d;
            maximum = 0d;
            for (int month = 0; month < MonthlySeries.MonthCount; month++)
            {
                double positive = data.Series.Sum(series => Math.Max(series.Values[month], 0d));
                double negative = data.Series.Sum(series => Math.Min(series.Values[month], 0d));
                minimum = Math.Min(minimum, negative);
                maximum = Math.Max(maximum, positive);
            }
        }
        else
        {
            minimum = Math.Min(0d, data.Series.SelectMany(series => series.Values).Min());
            maximum = Math.Max(0d, data.Series.SelectMany(series => series.Values).Max());
        }

        if (maximum <= minimum)
        {
            maximum = minimum + 1d;
        }

        return new PlotScale(minimum, maximum, height);
    }

    internal static Curve Line(
        MonthlySeries values,
        Plane plane,
        double width,
        PlotScale scale)
    {
        Point3d[] points = Enumerable.Range(0, MonthlySeries.MonthCount)
            .Select(month => plane.PointAt(
                width * month / (MonthlySeries.MonthCount - 1d),
                scale.Map(values[month])))
            .ToArray();
        return new PolylineCurve(points);
    }

    internal static GH_Structure<GH_Curve> Bars(
        GreenRetrofitMonthlyData data,
        Plane plane,
        double width,
        PlotScale scale,
        bool stacked)
    {
        var tree = new GH_Structure<GH_Curve>();
        double monthWidth = width / MonthlySeries.MonthCount;
        var positive = new double[MonthlySeries.MonthCount];
        var negative = new double[MonthlySeries.MonthCount];
        for (int seriesIndex = 0; seriesIndex < data.Series.Count; seriesIndex++)
        {
            var path = new GH_Path(seriesIndex);
            for (int month = 0; month < MonthlySeries.MonthCount; month++)
            {
                double value = data.Series[seriesIndex].Values[month];
                double x0;
                double x1;
                double y0;
                double y1;
                if (stacked)
                {
                    x0 = (month * monthWidth) + (monthWidth * 0.1d);
                    x1 = ((month + 1d) * monthWidth) - (monthWidth * 0.1d);
                    if (value >= 0d)
                    {
                        y0 = positive[month];
                        positive[month] += value;
                        y1 = positive[month];
                    }
                    else
                    {
                        y0 = negative[month];
                        negative[month] += value;
                        y1 = negative[month];
                    }
                }
                else
                {
                    double usable = monthWidth * 0.8d;
                    double barWidth = usable / data.Series.Count;
                    x0 = (month * monthWidth) + (monthWidth * 0.1d) + (seriesIndex * barWidth);
                    x1 = x0 + barWidth;
                    y0 = 0d;
                    y1 = value;
                }

                tree.Append(
                    new GH_Curve(Rectangle(plane, x0, x1, scale.Map(y0), scale.Map(y1))),
                    path);
            }
        }

        return tree;
    }

    internal static Curve Frame(Plane plane, double width, double height)
    {
        return Rectangle(plane, 0d, width, 0d, height);
    }

    internal static Curve ZeroAxis(Plane plane, double width, PlotScale scale)
    {
        double y = scale.Map(0d);
        return new LineCurve(plane.PointAt(0d, y), plane.PointAt(width, y));
    }

    private static PolylineCurve Rectangle(
        Plane plane,
        double x0,
        double x1,
        double y0,
        double y1)
    {
        return new PolylineCurve(new[]
        {
            plane.PointAt(x0, y0),
            plane.PointAt(x1, y0),
            plane.PointAt(x1, y1),
            plane.PointAt(x0, y1),
            plane.PointAt(x0, y0),
        });
    }
}
