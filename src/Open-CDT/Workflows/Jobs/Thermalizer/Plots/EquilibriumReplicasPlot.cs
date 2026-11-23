using Open_CDT.Statistics;
using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Thermalizer.Data;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.Thermalizer.Plots;

/// <summary>
/// Renders <c>thermalization_equilibrium_volumes.png</c> comparing trailing-window equilibrium
/// simplex volumes <c>&lt;N&gt;</c> across replicas against <see cref="ThermalizationResult.TargetVolume"/>.
/// </summary>
internal sealed class EquilibriumReplicasPlot : PlotBase<ThermalizationResult>
{
    /// <inheritdoc/>
    public override string FileName => "thermalization_equilibrium_volumes.png";

    /// <inheritdoc/>
    public override string Description => "Replica Equilibrium Volumes <N>";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, ThermalizationResult result)
    {
        var replicas = result.Replicas;
        if (replicas.Count == 0)
            return false;

        var xs = new double[replicas.Count];
        var simplices = new double[replicas.Count];
        var ticks = new Tick[replicas.Count];

        for (var i = 0; i < replicas.Count; i++)
        {
            xs[i] = i;
            simplices[i] = replicas[i].MeanSimplices;
            ticks[i] = new Tick(i, $"Rep {replicas[i].ReplicaId}");
        }

        (var meanN, var sdN, _) = SeriesStatistics.ComputeSample<double>(simplices);
        var targetVolume = (double)result.TargetVolume;

        if (sdN > 0.0)
        {
            var band = plt.Add.VerticalSpan(meanN - sdN, meanN + sdN);
            band.FillColor = PlotPalette.Blue.WithAlpha(0.14f);
            band.LegendText = $"Cross-Replica ±1 SD ({meanN:F2} ± {sdN:F2})";
        }

        var meanLine = plt.Add.HorizontalLine(meanN);
        meanLine.Color = PlotPalette.Blue;
        meanLine.LineWidth = 2.0f;
        meanLine.LinePattern = LinePattern.Dashed;
        meanLine.LegendText = $"Ensemble Mean <N> = {meanN:F2}";

        var targetLine = plt.Add.HorizontalLine(targetVolume);
        targetLine.Color = PlotPalette.Red;
        targetLine.LineWidth = 1.8f;
        targetLine.LinePattern = LinePattern.Dotted;
        targetLine.LegendText = $"Target Volume ({targetVolume:N0})";

        var scatter = plt.Add.Scatter(xs, simplices);
        scatter.Color = PlotPalette.Blue;
        scatter.LineWidth = 2.2f;
        scatter.MarkerSize = 9;
        scatter.MarkerShape = MarkerShape.FilledCircle;
        scatter.LegendText = "Replica Mean Simplices <N>";

        if (replicas.Count <= MaxAnnotatedReplicas)
        {
            for (var i = 0; i < replicas.Count; i++)
            {
                var r = replicas[i];
                AddPointLabel(
                    plt,
                    $"{r.MeanSimplices:F1}\n(N0={r.MeanVertices:F0})",
                    xs[i],
                    r.MeanSimplices,
                    placeAbove: r.MeanSimplices >= meanN,
                    verticalOffset: 10f);
            }
        }

        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);

        var rawMinY = Math.Min(targetVolume, Math.Min(meanN - sdN, simplices.Min()));
        var rawMaxY = Math.Max(targetVolume, Math.Max(meanN + sdN, simplices.Max()));
        var spanY = Math.Max(1.0, rawMaxY - rawMinY);

        plt.Axes.SetLimits(
            -0.6,
            replicas.Count - 0.4,
            Math.Max(0.0, rawMinY - (spanY * 0.35)),
            rawMaxY + (spanY * 0.45));

        plt.Title("Thermalized Replica Equilibrium Volumes");
        plt.XLabel("Replica Chain");
        plt.YLabel("Mean Simplex Volume <N>");
        plt.ShowLegend(Alignment.UpperRight);

        return true;
    }
}