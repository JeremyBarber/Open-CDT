using Open_CDT.Statistics;
using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Sampling.Data;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.Sampling.Plots;

/// <summary>
/// Renders <c>spectral_return_probability.png</c> displaying log-log ensemble heat-kernel
/// return probability <c>&lt;P(σ)&gt;</c> and its asymptotic IR power-law slope.
/// </summary>
internal sealed class SpectralReturnProbabilityPlot : PlotBase<SamplingResult>
{
    /// <inheritdoc/>
    public override string FileName => "spectral_return_probability.png";

    /// <inheritdoc/>
    public override string Description => "Heat-Kernel Return Probability P(σ)";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, SamplingResult result)
    {
        var samples = result.Samples;
        if (samples.Count == 0)
            return false;

        var maxSigma = samples[0].Spectral.ReturnProbabilities.Length - 1;
        if (maxSigma < 2)
            return false;

        var logSigmas = new List<double>(maxSigma);
        var logMeanP = new List<double>(maxSigma);
        var lowerBand = new List<double>(maxSigma);
        var upperBand = new List<double>(maxSigma);
        var irX = new List<double>(maxSigma);
        var irY = new List<double>(maxSigma);

        var irStartSigma = Math.Max(1, maxSigma / 2);
        var stepBuffer = new double[samples.Count];

        for (var s = 1; s <= maxSigma; s++)
        {
            for (var i = 0; i < samples.Count; i++)
                stepBuffer[i] = samples[i].Spectral.ReturnProbabilities[s];

            (var rawMean, var sd, _) = SeriesStatistics.ComputeSample<double>(stepBuffer);
            if (rawMean <= 0.0)
                continue;

            var logX = Math.Log10(s);
            var logMean = Math.Log10(rawMean);
            var logUpper = Math.Log10(rawMean + sd);
            var logLower = rawMean > sd
                ? Math.Log10(rawMean - sd)
                : (2.0 * logMean) - logUpper;

            logSigmas.Add(logX);
            logMeanP.Add(logMean);
            lowerBand.Add(logLower);
            upperBand.Add(logUpper);

            if (s >= irStartSigma)
            {
                irX.Add(logX);
                irY.Add(logMean);
            }
        }

        if (logSigmas.Count < 2)
            return false;

        var xs = logSigmas.ToArray();
        var ys = logMeanP.ToArray();
        var lowers = lowerBand.ToArray();
        var uppers = upperBand.ToArray();

        (var irSlope, var irIntercept) = SeriesStatistics.FitLinear(irX.ToArray(), irY.ToArray());
        var fittedDsIr = -2.0 * irSlope;

        var xIr0 = Math.Log10(irStartSigma);
        var xIr1 = Math.Log10(maxSigma);

        var irSpan = plt.Add.HorizontalSpan(xIr0, xIr1);
        irSpan.FillColor = PlotPalette.Purple.WithAlpha(0.13f);
        irSpan.LegendText = $"Asymptotic IR Fit Window σ in [{irStartSigma}, {maxSigma}]";

        var fill = plt.Add.FillY(xs, lowers, uppers);
        fill.FillColor = PlotPalette.Blue.WithAlpha(0.15f);
        fill.LineWidth = 0;
        fill.LegendText = "Sample Fluctuation Band (±1σ)";

        var curve = plt.Add.Scatter(xs, ys);
        curve.Color = PlotPalette.Blue;
        curve.LineWidth = 2.0f;
        curve.MarkerSize = 4.5f;
        curve.MarkerShape = MarkerShape.FilledCircle;
        curve.LegendText = "Ensemble Return Probability log10 <P(σ)>";

        AddLineSegment(
            plt,
            xIr0,
            (irSlope * xIr0) + irIntercept,
            xIr1,
            (irSlope * xIr1) + irIntercept,
            PlotPalette.Red,
            2.8f,
            LinePattern.Dashed,
            $"IR Power-Law Fit (slope = {irSlope:F3} → D_S = {fittedDsIr:F2})");

        var padX = Math.Max(0.04, (xs[^1] - xs[0]) * 0.05);
        var minY = lowers.Min();
        var maxY = uppers.Max();
        var padY = Math.Max(0.08, (maxY - minY) * 0.08);

        plt.Axes.SetLimits(xs[0] - padX, xs[^1] + padX, minY - padY, maxY + padY);

        plt.Title($"Heat-Kernel Return Probability <P(σ)> (Asymptotic IR Fit D_S = {fittedDsIr:F2})");
        plt.XLabel("log10(σ) — Diffusion Step");
        plt.YLabel("log10 <P(σ)> — Return Probability");
        plt.ShowLegend(Alignment.UpperRight);

        return true;
    }
}