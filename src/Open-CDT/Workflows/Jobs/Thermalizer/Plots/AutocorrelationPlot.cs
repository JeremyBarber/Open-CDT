using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Thermalizer.Data;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.Thermalizer.Plots;

/// <summary>
/// Renders <c>thermalization_autocorrelation.png</c> displaying per-replica Madras-Sokal integrated
/// autocorrelation times (<c>tau_int</c>) and the ensemble decorrelation ceiling.
/// </summary>
internal sealed class AutocorrelationPlot : PlotBase<ThermalizationResult>
{
    /// <inheritdoc/>
    public override string FileName => "thermalization_autocorrelation.png";

    /// <inheritdoc/>
    public override string Description => "Integrated Autocorrelation τ_int";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, ThermalizationResult result)
    {
        var replicas = result.Replicas;
        if (replicas.Count == 0)
            return false;

        var bars = new List<Bar>(replicas.Count);
        var ticks = new Tick[replicas.Count];
        var annotate = replicas.Count <= MaxAnnotatedReplicas;

        for (var i = 0; i < replicas.Count; i++)
        {
            var r = replicas[i];
            var barColor = r.TauConverged ? PlotPalette.Green : PlotPalette.Amber;

            bars.Add(new Bar
            {
                Position = i,
                Value = r.TauInt,
                FillColor = barColor.WithAlpha(0.85f),
                LineColor = barColor,
                LineWidth = 1.5f
            });

            ticks[i] = new Tick(i, $"Rep {r.ReplicaId}");

            if (annotate)
            {
                AddPointLabel(plt, $"{r.TauInt}", i, r.TauInt, placeAbove: true, verticalOffset: 4f, fontSize: 11f);
            }
        }

        plt.Add.Bars(bars);
        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);

        var meanTau = replicas.Average(r => r.TauInt);
        var meanLine = plt.Add.HorizontalLine(meanTau);
        meanLine.Color = PlotPalette.Blue;
        meanLine.LineWidth = 2.0f;
        meanLine.LinePattern = LinePattern.Dashed;
        meanLine.LegendText = $"Mean τ_int = {meanTau:F1} sweeps";

        var maxLine = plt.Add.HorizontalLine(result.EnsembleTauInt);
        maxLine.Color = PlotPalette.Red;
        maxLine.LineWidth = 2.2f;
        maxLine.LinePattern = LinePattern.Dotted;
        maxLine.LegendText = $"Ensemble Ceiling τ_int = {result.EnsembleTauInt} (2τ = {2 * result.EnsembleTauInt} sweeps)";

        if (replicas.Any(r => r.TauConverged))
        {
            AddLegendSwatch(plt, PlotPalette.Green, "Madras-Sokal Converged (M >= 6τ_int)");
        }

        if (replicas.Any(r => !r.TauConverged))
        {
            AddLegendSwatch(plt, PlotPalette.Amber, "Lag Cutoff Reached (M < 6τ_int)");
        }

        plt.Axes.SetLimits(-0.6, replicas.Count - 0.4, 0.0, result.EnsembleTauInt * 1.38);
        plt.Title($"Replica Integrated Autocorrelation Time τ_int (Ensemble Ceiling = {result.EnsembleTauInt} Sweeps)");
        plt.XLabel("Replica Chain");
        plt.YLabel("Integrated Autocorrelation Time τ_int (Sweeps)");
        plt.ShowLegend(Alignment.UpperRight);

        return true;
    }
}