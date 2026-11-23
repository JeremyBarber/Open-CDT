using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;
using Open_CDT.Workflows.Plots;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Plots;

/// <summary>
/// Renders <c>tuning_calibration.png</c> showing equilibrium volume <c>&lt;N&gt;</c> versus <c>k_d</c>
/// (or universal offset <c>Δk_d = k_d - k_d^c</c>) alongside the analytical saddle-point susceptibility curve.
/// </summary>
internal sealed class CalibrationCurvePlot : PlotBase<TuningResult>
{
    private const double AxisMarginFraction = 0.06;

    /// <inheritdoc/>
    public override string FileName => "tuning_calibration.png";

    /// <inheritdoc/>
    public override string Description => "Phase-Space Calibration Curve";

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

        var targetVolume = (double)result.TargetVolume;
        var refX = isMultiCoord ? 0.0 : result.TunedKd;
        var slope = result.SusceptibilitySlope;
        var meanSigma = result.CriticalPoints.Count > 0
            ? result.CriticalPoints.Average(c => c.StdDevVolume)
            : history.Average(p => p.StdDevVolume);

        var allXs = history.Select(p => ResolveProbeX(p, kdCritLookup)).ToArray();
        var minX = Math.Min(refX, allXs.Min());
        var maxX = Math.Max(refX, allXs.Max());
        var padX = Math.Max(result.ToleranceDeltaKd, (maxX - minX) * AxisMarginFraction);
        var x0 = minX - padX;
        var x1 = maxX + padX;

        AddLineSegment(
            plt,
            x0,
            targetVolume + (slope * (x0 - refX)),
            x1,
            targetVolume + (slope * (x1 - refX)),
            PlotPalette.Slate.WithAlpha(0.65f),
            2.0f,
            LinePattern.Dashed,
            $"Saddle-Point Susceptibility (-1/2ε = {slope:F1}/kd, <σ_N> = {meanSigma:F1} vs {result.TheoreticalStdDev:F1})");

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

        PlotOutcomeGroup(plt, history, kdCritLookup, ProbeOutcome.AboveTarget, PlotPalette.Amber, "Overvolume (Above Target)", markerSize: 5f);
        PlotOutcomeGroup(plt, history, kdCritLookup, ProbeOutcome.LockedOnTarget, PlotPalette.Green, "Locked On Target", markerSize: 5f);
        PlotOutcomeGroup(plt, history, kdCritLookup, ProbeOutcome.BelowTarget, PlotPalette.Blue, "Undervolume (Below Target)", markerSize: 5f);

        var rawMinY = Math.Min(targetVolume, history.Min(p => p.MeanVolume - p.StdDevVolume));
        var rawMaxY = Math.Max(targetVolume, history.Max(p => p.MeanVolume + p.StdDevVolume));
        var padY = Math.Max(2.0 * result.TheoreticalStdDev, (rawMaxY - rawMinY) * AxisMarginFraction);
        plt.Axes.SetLimits(x0, x1, Math.Max(0.0, rawMinY - padY), rawMaxY + padY);

        plt.Title(isMultiCoord
            ? $"Universal Susceptibility Calibration ({history.Count} Probes Across {result.CriticalPoints.Count} Coordinates)"
            : $"Cosmological Constant Calibration Curve ({history.Count} Completed Probes)");
        plt.XLabel(isMultiCoord
            ? (hasMultipleDeltas ? "Offset from Critical Coupling Δkd = kd - kd_c(k0, Δ)" : "Offset from Critical Coupling Δkd = kd - kd_c(k0)")
            : "Cosmological Constant (kd)");
        plt.YLabel("Equilibrium Simplex Volume <N>");
        plt.ShowLegend(Alignment.UpperRight);

        return true;
    }

    internal static double ResolveProbeX(
        ProbeRecord probe,
        Dictionary<(double K0, double Delta), double>? kdCritLookup) =>
        kdCritLookup is not null && kdCritLookup.TryGetValue((probe.K0, probe.Delta), out var kc)
            ? probe.Kd - kc
            : probe.Kd;

    internal static void PlotOutcomeGroup(
        Plot plt,
        IReadOnlyList<ProbeRecord> history,
        Dictionary<(double K0, double Delta), double>? kdCritLookup,
        ProbeOutcome outcome,
        Color color,
        string label,
        bool annotatePoints = false,
        float markerSize = 9f)
    {
        var group = history.Where(p => p.Outcome == outcome).ToArray();
        if (group.Length == 0)
            return;

        var xs = group.Select(p => ResolveProbeX(p, kdCritLookup)).ToArray();
        var ys = group.Select(p => p.MeanVolume).ToArray();
        var errs = group.Select(p => p.StdDevVolume).ToArray();

        var errBars = plt.Add.ErrorBar(xs, ys, errs);
        errBars.Color = color;
        errBars.LineWidth = 1.8f;

        var scatter = plt.Add.Scatter(xs, ys);
        scatter.Color = color;
        scatter.LineWidth = 0;
        scatter.MarkerSize = markerSize;
        scatter.MarkerShape = MarkerShape.FilledCircle;
        scatter.LegendText = $"{label} (n={group.Length})";

        if (annotatePoints)
        {
            for (var i = 0; i < group.Length; i++)
            {
                var p = group[i];
                var txt = plt.Add.Text(
                    $"k0={p.K0:F1} ({p.MeanVolume:F0}±{p.StdDevVolume:F0})",
                    xs[i],
                    p.MeanVolume);
                txt.LabelFontColor = PlotPalette.Slate;
                txt.LabelFontSize = 10;
                txt.LabelAlignment = Alignment.MiddleLeft;
                txt.OffsetX = 10;
                txt.OffsetY = outcome == ProbeOutcome.BelowTarget ? 10 : -10;
            }
        }
    }
}