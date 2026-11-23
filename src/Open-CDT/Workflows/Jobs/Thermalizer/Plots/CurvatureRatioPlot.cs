using Open_CDT.Statistics;
using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Thermalizer.Data;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.Thermalizer.Plots;

/// <summary>
/// Renders <c>thermalization_curvature_ratio.png</c> comparing trailing-window vertex-to-simplex
/// ratios <c>&lt;N_0&gt; / &lt;N&gt;</c> across thermalized replica chains.
/// </summary>
internal sealed class CurvatureRatioPlot : PlotBase<ThermalizationResult>
{
    /// <inheritdoc/>
    public override string FileName => "thermalization_curvature_ratio.png";

    /// <inheritdoc/>
    public override string Description => "Vertex-to-Simplex Ratio <N0/N>";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, ThermalizationResult result)
    {
        var replicas = result.Replicas;
        if (replicas.Count == 0)
            return false;

        var xs = new double[replicas.Count];
        var ratios = new double[replicas.Count];
        var ticks = new Tick[replicas.Count];

        for (var i = 0; i < replicas.Count; i++)
        {
            xs[i] = i;
            ratios[i] = replicas[i].VertexDensity;
            ticks[i] = new Tick(i, $"Rep {replicas[i].ReplicaId}");
        }

        (var meanRatio, var sdRatio, _) = SeriesStatistics.ComputeSample<double>(ratios);

        if (sdRatio > 0.0)
        {
            var band = plt.Add.VerticalSpan(meanRatio - sdRatio, meanRatio + sdRatio);
            band.FillColor = PlotPalette.Purple.WithAlpha(0.14f);
            band.LegendText = $"Ensemble ±1 SD ({meanRatio:F6} ± {sdRatio:F6})";
        }

        var meanLine = plt.Add.HorizontalLine(meanRatio);
        meanLine.Color = PlotPalette.Purple;
        meanLine.LineWidth = 2.0f;
        meanLine.LinePattern = LinePattern.Dashed;
        meanLine.LegendText = $"Mean Ratio <N0 / N> = {meanRatio:F6}";

        var scatter = plt.Add.Scatter(xs, ratios);
        scatter.Color = PlotPalette.Purple;
        scatter.LineWidth = 2.2f;
        scatter.MarkerSize = 9;
        scatter.MarkerShape = MarkerShape.FilledDiamond;
        scatter.LegendText = "Replica Vertex Ratio <N0> / <N>";

        if (replicas.Count <= MaxAnnotatedReplicas)
        {
            for (var i = 0; i < replicas.Count; i++)
            {
                AddPointLabel(
                    plt,
                    $"{ratios[i]:F5}",
                    xs[i],
                    ratios[i],
                    placeAbove: ratios[i] >= meanRatio,
                    verticalOffset: 10f);
            }
        }

        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);

        var rawMinY = Math.Min(meanRatio - sdRatio, ratios.Min());
        var rawMaxY = Math.Max(meanRatio + sdRatio, ratios.Max());
        var spanY = rawMaxY > rawMinY ? rawMaxY - rawMinY : Math.Max(1e-4, meanRatio * 0.01);

        plt.Axes.SetLimits(
            -0.6,
            replicas.Count - 0.4,
            Math.Max(0.0, rawMinY - (spanY * 0.45)),
            rawMaxY + (spanY * 0.35));

        plt.Title("Internal Curvature: Vertex Ratio <N0>/<N>");
        plt.XLabel("Replica Chain");
        plt.YLabel("Vertex-to-Simplex Ratio (<N0> / <N>)");
        plt.ShowLegend(Alignment.LowerRight);

        return true;
    }
}