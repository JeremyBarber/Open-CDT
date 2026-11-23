using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;
using Open_CDT.Workflows.Plots;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Plots;

/// <summary>
/// Renders <c>tuning_bracket_zoom.png</c> zooming into the near-critical tolerance corridor around <c>k_d^c</c>.
/// </summary>
internal sealed class BracketZoomPlot : PlotBase<TuningResult>
{
    private const int MaxSingleCoordZoomProbes = 4;
    private const int MaxAnnotatedProbes = 6;

    /// <inheritdoc/>
    public override string FileName => "tuning_bracket_zoom.png";

    /// <inheritdoc/>
    public override string Description => "Near-Critical Convergence";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, TuningResult result)
    {
        var history = result.History;
        if (history.Count == 0)
            return false;

        var isMultiCoord = result.CriticalPoints.Count > 1;
        var hasMultipleDeltas = result.CriticalPoints.Select(c => c.Delta).Distinct().Count() > 1;
        var kdCritLookup = isMultiCoord
            ? result.CriticalPoints.ToDictionary(c => (c.K0, c.Delta), c => c.KdCrit)
            : null;

        var refX = isMultiCoord ? 0.0 : result.TunedKd;

        var nearProbes = isMultiCoord
            ? history.Where(p => p.Outcome == ProbeOutcome.LockedOnTarget)
                .OrderBy(p => CalibrationCurvePlot.ResolveProbeX(p, kdCritLookup))
                .ToArray()
            : history.OrderBy(p => Math.Abs(p.Kd - result.TunedKd))
                .Take(MaxSingleCoordZoomProbes)
                .OrderBy(p => p.Kd)
                .ToArray();

        if (nearProbes.Length == 0)
        {
            nearProbes = history
                .OrderBy(p => Math.Abs(CalibrationCurvePlot.ResolveProbeX(p, kdCritLookup) - refX))
                .Take(MaxAnnotatedProbes)
                .OrderBy(p => CalibrationCurvePlot.ResolveProbeX(p, kdCritLookup))
                .ToArray();
        }

        var targetVolume = (double)result.TargetVolume;
        var tolerance = targetVolume * result.ToleranceRatio;
        var slope = result.SusceptibilitySlope;

        var band = plt.Add.VerticalSpan(targetVolume - tolerance, targetVolume + tolerance);
        band.FillColor = PlotPalette.Green.WithAlpha(0.14f);
        band.LegendText = $"Target Lock Band ({targetVolume:N0} ± {result.ToleranceRatio * 100.0:G3}%)";

        var nearXs = nearProbes.Select(p => CalibrationCurvePlot.ResolveProbeX(p, kdCritLookup)).ToArray();
        var minX = Math.Min(refX - result.ToleranceDeltaKd, nearXs.Min());
        var maxX = Math.Max(refX + result.ToleranceDeltaKd, nearXs.Max());
        var spanX = Math.Max(2.0 * result.ToleranceDeltaKd, maxX - minX);
        var x0 = minX - (spanX * 0.25);
        var x1 = maxX + (spanX * 0.35);

        AddLineSegment(
            plt,
            x0,
            targetVolume + (slope * (x0 - refX)),
            x1,
            targetVolume + (slope * (x1 - refX)),
            PlotPalette.Slate.WithAlpha(0.65f),
            2.0f,
            LinePattern.Dashed,
            "Saddle-Point Susceptibility (-1/2ε)");

        var hLine = plt.Add.HorizontalLine(targetVolume);
        hLine.Color = PlotPalette.Red;
        hLine.LineWidth = 1.8f;
        hLine.LinePattern = LinePattern.Dotted;
        hLine.LegendText = $"Target Volume ({targetVolume:N0})";

        var vLine = plt.Add.VerticalLine(refX);
        vLine.Color = PlotPalette.Green;
        vLine.LineWidth = 1.8f;
        vLine.LinePattern = LinePattern.DenselyDashed;
        vLine.LegendText = isMultiCoord
            ? "Critical Surface (Δkd = 0)"
            : $"Locked kd = {result.TunedKd:F5}";

        var annotate = nearProbes.Length <= MaxAnnotatedProbes;
        CalibrationCurvePlot.PlotOutcomeGroup(plt, nearProbes, kdCritLookup, ProbeOutcome.AboveTarget, PlotPalette.Amber, "Overvolume (Above Target)", annotatePoints: annotate, markerSize: 6f);
        CalibrationCurvePlot.PlotOutcomeGroup(plt, nearProbes, kdCritLookup, ProbeOutcome.LockedOnTarget, PlotPalette.Green, "Locked On Target", annotatePoints: annotate, markerSize: 6f);
        CalibrationCurvePlot.PlotOutcomeGroup(plt, nearProbes, kdCritLookup, ProbeOutcome.BelowTarget, PlotPalette.Blue, "Undervolume (Below Target)", annotatePoints: annotate, markerSize: 6f);

        var rawMinY = Math.Min(targetVolume - tolerance, nearProbes.Min(p => p.MeanVolume - p.StdDevVolume));
        var rawMaxY = Math.Max(targetVolume + tolerance, nearProbes.Max(p => p.MeanVolume + p.StdDevVolume));
        var padY = Math.Max(result.TheoreticalStdDev, (rawMaxY - rawMinY) * 0.15);
        plt.Axes.SetLimits(x0, x1, Math.Max(0.0, rawMinY - padY), rawMaxY + padY);

        plt.Title(isMultiCoord
            ? $"Zoomed Locked Critical Band Around Δkd = 0 ({nearProbes.Length} Probes)"
            : $"Near-Critical Convergence Around kd = {result.TunedKd:F5}");
        plt.XLabel(isMultiCoord
            ? (hasMultipleDeltas ? "Offset from Critical Coupling Δkd = kd - kd_c(k0, Δ)" : "Offset from Critical Coupling Δkd = kd - kd_c(k0)")
            : "Cosmological Constant (kd)");
        plt.YLabel("Equilibrium Simplex Volume <N>");
        plt.ShowLegend(Alignment.UpperRight);

        return true;
    }
}