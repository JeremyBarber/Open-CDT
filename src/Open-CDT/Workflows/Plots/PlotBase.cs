using ScottPlot;

namespace Open_CDT.Workflows.Plots;

/// <summary>
/// Shared base class for all typed dataset plots, standardizing canvas styling, image dimensions,
/// and common geometric/annotation primitives across jobs.
/// </summary>
/// <typeparam name="TResult">Strongly-typed job output dataset rendered by this plot.</typeparam>
internal abstract class PlotBase<TResult>
    where TResult : class
{
    public const int ImageWidth = 1280;
    public const int ImageHeight = 800;
    protected const int MaxAnnotatedReplicas = 16;
    protected const int MaxSeriesSlices = 8;

    /// <summary>
    /// Output PNG filename written inside the run directory.
    /// </summary>
    public abstract string FileName { get; }

    /// <summary>
    /// Concise human-readable chart description displayed in CLI plotting progress rows.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Creates a standardized <see cref="Plot"/> canvas, populates plottables via <see cref="TryBuild"/>,
    /// and saves the PNG to <paramref name="directory"/> when applicable.
    /// </summary>
    public void Render(string directory, TResult result)
    {
        var plt = CreateCanvas();
        if (TryBuild(plt, result))
        {
            plt.SavePng(Path.Combine(directory, FileName), ImageWidth, ImageHeight);
        }
    }

    /// <summary>
    /// Populates <paramref name="plt"/> from <paramref name="result"/>. Returns <c>false</c> to skip saving
    /// when the dataset lacks sufficient dimensionality or points for this chart.
    /// </summary>
    protected abstract bool TryBuild(Plot plt, TResult result);

    private static Plot CreateCanvas()
    {
        var plt = new Plot { ScaleFactor = 1.6f };
        plt.FigureBackground.Color = PlotPalette.Canvas;
        plt.DataBackground.Color = PlotPalette.Canvas;
        plt.Grid.MajorLineColor = PlotPalette.GridMajor;
        return plt;
    }

    protected static void AddLineSegment(
        Plot plt,
        double x0,
        double y0,
        double x1,
        double y1,
        Color color,
        float lineWidth,
        LinePattern pattern,
        string legendText = "")
    {
        double[] xs = [x0, x1];
        double[] ys = [y0, y1];
        var line = plt.Add.Scatter(xs, ys);
        line.Color = color;
        line.LineWidth = lineWidth;
        line.LinePattern = pattern;
        line.MarkerSize = 0;
        if (legendText.Length > 0)
            line.LegendText = legendText;
    }

    protected static void AddSingleMarker(
        Plot plt,
        double x,
        double y,
        Color color,
        MarkerShape shape,
        float markerSize,
        string legendText = "")
    {
        double[] xs = [x];
        double[] ys = [y];
        var marker = plt.Add.Scatter(xs, ys);
        marker.Color = color;
        marker.LineWidth = 0;
        marker.MarkerShape = shape;
        marker.MarkerSize = markerSize;
        if (legendText.Length > 0)
            marker.LegendText = legendText;
    }

    protected static void AddLegendSwatch(
        Plot plt,
        Color color,
        string legendText,
        MarkerShape shape = MarkerShape.FilledSquare,
        float markerSize = 10f)
    {
        var proxy = plt.Add.Scatter([], []);
        proxy.Color = color;
        proxy.LineWidth = 0;
        proxy.MarkerShape = shape;
        proxy.MarkerSize = markerSize;
        proxy.LegendText = legendText;
    }

    protected static void AddVerticalBandPolygon(
        Plot plt,
        IReadOnlyList<double> xs,
        IReadOnlyList<double> lowerYs,
        IReadOnlyList<double> upperYs,
        Color color,
        float alpha,
        string legendText = "")
    {
        var n = xs.Count;
        var coords = new Coordinates[n * 2];

        for (var i = 0; i < n; i++)
            coords[i] = new Coordinates(xs[i], lowerYs[i]);

        for (var i = 0; i < n; i++)
            coords[n + i] = new Coordinates(xs[n - 1 - i], upperYs[n - 1 - i]);

        var poly = plt.Add.Polygon(coords);
        poly.FillColor = color.WithAlpha(alpha);
        poly.LineWidth = 0;
        if (legendText.Length > 0)
            poly.LegendText = legendText;
    }

    protected static void AddHorizontalBandPolygon(
        Plot plt,
        IReadOnlyList<double> leftXs,
        IReadOnlyList<double> leftYs,
        IReadOnlyList<double> rightXs,
        IReadOnlyList<double> rightYs,
        Color color,
        float alpha,
        string legendText = "")
    {
        var n = leftXs.Count;
        var coords = new Coordinates[n * 2];

        for (var i = 0; i < n; i++)
            coords[i] = new Coordinates(leftXs[i], leftYs[i]);

        for (var i = 0; i < n; i++)
            coords[n + i] = new Coordinates(rightXs[n - 1 - i], rightYs[n - 1 - i]);

        var poly = plt.Add.Polygon(coords);
        poly.FillColor = color.WithAlpha(alpha);
        poly.LineWidth = 0;
        if (legendText.Length > 0)
            poly.LegendText = legendText;
    }

    protected static void AddPointLabel(
        Plot plt,
        string text,
        double x,
        double y,
        bool placeAbove,
        float verticalOffset = 12f,
        float fontSize = 10f)
    {
        var label = plt.Add.Text(text, x, y);
        label.LabelFontColor = PlotPalette.Slate;
        label.LabelFontSize = fontSize;
        label.LabelAlignment = placeAbove ? Alignment.LowerCenter : Alignment.UpperCenter;
        label.OffsetY = placeAbove ? -verticalOffset : verticalOffset;
    }

    protected static T[] SelectEvenlySpaced<T>(T[] items, int maxCount = MaxSeriesSlices)
    {
        if (items.Length <= maxCount)
            return items;

        var selected = new T[maxCount];
        for (var i = 0; i < maxCount; i++)
        {
            var idx = (int)Math.Round(i * (items.Length - 1.0) / (maxCount - 1.0));
            selected[i] = items[idx];
        }

        return selected;
    }

    protected static string FormatSuperscript(uint exponent) => exponent switch
    {
        1 => "¹",
        2 => "²",
        3 => "³",
        4 => "⁴",
        _ => $"^{exponent}"
    };
}