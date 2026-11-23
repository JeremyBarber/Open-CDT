using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Sampling.Data;
using Open_CDT.Workflows.Jobs.Sampling.Helpers;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.Sampling.Plots;

/// <summary>
/// Renders <c>spectral_dimension_running.png</c> displaying the scale-dependent spectral dimension
/// <c>D_S(σ)</c>, leave-one-replica-out jackknife error band, and AJL rational asymptote fit.
/// </summary>
internal sealed class SpectralDimensionRunningPlot : PlotBase<SamplingResult>
{
    /// <inheritdoc/>
    public override string FileName => "spectral_dimension_running.png";

    /// <inheritdoc/>
    public override string Description => "Running Spectral Dimension D_S(σ)";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, SamplingResult result)
    {
        (var xs, var ys, var semDs, var nReplicas) = SpectralFlowAnalysis.ComputeRunningCurve(result.Samples);
        if (xs.Length == 0)
            return false;

        var dSpace = result.SpacelikeDimensions;
        var classicalDim = dSpace + 1.0;

        var lower = new double[xs.Length];
        var upper = new double[xs.Length];
        for (var i = 0; i < xs.Length; i++)
        {
            lower[i] = ys[i] - semDs[i];
            upper[i] = ys[i] + semDs[i];
        }

        (var dInf, var bAjl, var cAjl) = SpectralFlowAnalysis.FitAjlCurve(xs, ys);
        var ajlY = xs.Select(s => dInf - (bAjl / (cAjl + s))).ToArray();

        if (nReplicas > 1)
        {
            var fill = plt.Add.FillY(xs, lower, upper);
            fill.FillColor = PlotPalette.Orange.WithAlpha(0.20f);
            fill.LineWidth = 0;
            fill.LegendText = $"Jackknife ±1 SEM Band Across {nReplicas} Replicas";
        }

        var irLine = plt.Add.HorizontalLine(classicalDim);
        irLine.Color = PlotPalette.Green;
        irLine.LineWidth = 2.0f;
        irLine.LinePattern = LinePattern.Dashed;
        irLine.LegendText = $"Classical {dSpace}+1D Spacetime Limit (D_S = {classicalDim:F2})";

        var uvLine = plt.Add.HorizontalLine(2.0);
        uvLine.Color = PlotPalette.Blue;
        uvLine.LineWidth = 2.0f;
        uvLine.LinePattern = LinePattern.Dotted;
        uvLine.LegendText = "Short-Scale UV Limit (D_S = 2.00)";

        var ajlCurve = plt.Add.Scatter(xs, ajlY);
        ajlCurve.Color = PlotPalette.Ink;
        ajlCurve.LineWidth = 2.4f;
        ajlCurve.LinePattern = LinePattern.Dashed;
        ajlCurve.MarkerSize = 0;
        ajlCurve.LegendText = $"AJL Fit: D_S(σ) = {dInf:F2} - {bAjl:F1}/({cAjl:F1} + σ)";

        var curve = plt.Add.Scatter(xs, ys);
        curve.Color = PlotPalette.Orange;
        curve.LineWidth = 2.2f;
        curve.MarkerSize = 5;
        curve.MarkerShape = MarkerShape.FilledCircle;
        curve.LegendText = "Running D_S(σ) = -2 d ln<P(σ)> / d ln σ";

        var rawMinY = Math.Min(2.0, lower.Min());
        var rawMaxY = Math.Max(classicalDim, upper.Max());
        var spanY = Math.Max(0.5, rawMaxY - rawMinY);

        plt.Axes.SetLimits(
            0.0,
            xs[^1] * 1.06,
            Math.Max(0.0, rawMinY - (spanY * 0.28)),
            rawMaxY + (spanY * 0.22));

        plt.Title($"Dynamic Dimensional Reduction D_S(σ) (UV = {ys[0]:F2} → IR Asymptote D_inf = {dInf:F2})");
        plt.XLabel("Diffusion Step (σ)");
        plt.YLabel("Running Spectral Dimension D_S(σ)");
        plt.ShowLegend(Alignment.LowerRight);

        return true;
    }
}