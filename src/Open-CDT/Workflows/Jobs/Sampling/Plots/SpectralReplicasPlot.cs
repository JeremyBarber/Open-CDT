using Open_CDT.Statistics;
using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Sampling.Data;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.Sampling.Plots;

/// <summary>
/// Renders <c>spectral_replicas.png</c> comparing per-replica macroscopic spectral dimension
/// estimates <c>&lt;D_S&gt; ± SEM</c> against the classical <c>(d_s + 1)</c> spacetime limit.
/// </summary>
internal sealed class SpectralReplicasPlot : PlotBase<SamplingResult>
{
    /// <inheritdoc/>
    public override string FileName => "spectral_replicas.png";

    /// <inheritdoc/>
    public override string Description => "Cross-Replica Spectral Dimension";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, SamplingResult result)
    {
        var samples = result.Samples;
        if (samples.Count == 0)
            return false;

        var replicaGroups = samples
            .GroupBy(s => s.ReplicaId)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var vals = g
                    .Select(s => s.Spectral.EstimatedDimension)
                    .Where(double.IsFinite)
                    .ToArray();

                if (vals.Length == 0)
                    return (ReplicaId: g.Key, Mean: double.NaN, Sem: 0.0, Count: 0);

                (var mean, _, var sem) = SeriesStatistics.ComputeSample<double>(vals);
                return (ReplicaId: g.Key, Mean: mean, Sem: sem, Count: vals.Length);
            })
            .Where(r => r.Count > 0)
            .ToArray();

        if (replicaGroups.Length == 0)
            return false;

        var dSpace = result.SpacelikeDimensions;
        var classicalDim = dSpace + 1.0;

        var xs = new double[replicaGroups.Length];
        var means = new double[replicaGroups.Length];
        var sems = new double[replicaGroups.Length];
        var ticks = new Tick[replicaGroups.Length];

        for (var i = 0; i < replicaGroups.Length; i++)
        {
            xs[i] = i;
            means[i] = replicaGroups[i].Mean;
            sems[i] = replicaGroups[i].Sem;
            ticks[i] = new Tick(i, $"Rep {replicaGroups[i].ReplicaId}");
        }

        (var grandMean, var grandSd, _) = SeriesStatistics.ComputeSample<double>(means);

        if (grandSd > 0.0)
        {
            var band = plt.Add.VerticalSpan(grandMean - grandSd, grandMean + grandSd);
            band.FillColor = PlotPalette.Orange.WithAlpha(0.14f);
            band.LegendText = $"Cross-Replica ±1 SD ({grandMean:F3} ± {grandSd:F3})";
        }

        var meanLine = plt.Add.HorizontalLine(grandMean);
        meanLine.Color = PlotPalette.Orange;
        meanLine.LineWidth = 2.0f;
        meanLine.LinePattern = LinePattern.Dashed;
        meanLine.LegendText = $"Ensemble Mean D_S = {grandMean:F3}";

        var targetLine = plt.Add.HorizontalLine(classicalDim);
        targetLine.Color = PlotPalette.Green;
        targetLine.LineWidth = 2.0f;
        targetLine.LinePattern = LinePattern.Dotted;
        targetLine.LegendText = $"Classical {dSpace}+1D Limit (D_S = {classicalDim:F2})";

        var errBars = plt.Add.ErrorBar(xs, means, sems);
        errBars.Color = PlotPalette.Purple;
        errBars.LineWidth = 1.8f;

        var samplesPerRep = replicaGroups[0].Count;
        var scatter = plt.Add.Scatter(xs, means);
        scatter.Color = PlotPalette.Purple;
        scatter.LineWidth = 2.2f;
        scatter.MarkerSize = 9;
        scatter.MarkerShape = MarkerShape.FilledCircle;
        scatter.LegendText = $"Replica Mean D_S ± SEM (n = {samplesPerRep}/rep)";

        if (replicaGroups.Length <= MaxAnnotatedReplicas)
        {
            for (var i = 0; i < replicaGroups.Length; i++)
            {
                AddPointLabel(plt, $"{means[i]:F3}", xs[i], means[i], placeAbove: means[i] >= grandMean);
            }
        }

        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);

        var rawMinY = Math.Min(classicalDim, Math.Min(grandMean - grandSd, replicaGroups.Min(r => r.Mean - r.Sem)));
        var rawMaxY = Math.Max(classicalDim, Math.Max(grandMean + grandSd, replicaGroups.Max(r => r.Mean + r.Sem)));
        var spanY = Math.Max(0.20, rawMaxY - rawMinY);

        plt.Axes.SetLimits(
            -0.6,
            replicaGroups.Length - 0.4,
            Math.Max(0.0, rawMinY - (spanY * 0.35)),
            rawMaxY + (spanY * 0.45));

        plt.Title($"Macroscopic Spectral Dimension D_S by Replica (Mean = {grandMean:F3} ± {grandSd:F3})");
        plt.XLabel("Replica Chain");
        plt.YLabel("Estimated Macroscopic Spectral Dimension D_S");
        plt.ShowLegend(Alignment.UpperLeft);

        return true;
    }
}