using Open_CDT.Statistics;
using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Sampling.Data;
using Open_CDT.Workflows.Jobs.Sampling.Helpers;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.Sampling.Plots;

/// <summary>
/// Renders <c>volume_profile_ensemble.png</c> overlaying the centered ensemble proper-time volume
/// profile <c>&lt;N(t)&gt;</c> against the continuum <c>cos^{d_s}</c> spherical droplet fit.
/// </summary>
internal sealed class VolumeProfileEnsemblePlot : PlotBase<SamplingResult>
{
    /// <inheritdoc/>
    public override string FileName => "volume_profile_ensemble.png";

    /// <inheritdoc/>
    public override string Description => "Ensemble Proper-Time Profile";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, SamplingResult result)
    {
        var samples = result.Samples;
        if (samples.Count == 0)
            return false;

        var nSamples = samples.Count;
        var timeslices = samples[0].CenteredVolumeProfile.Length;
        if (timeslices == 0)
            return false;

        var spacelikeDims = result.SpacelikeDimensions;

        var ts = new double[timeslices];
        var meanProfile = new double[timeslices];
        var stdProfile = new double[timeslices];
        var semProfile = new double[timeslices];
        var sliceBuffer = new double[nSamples];

        for (var t = 0; t < timeslices; t++)
        {
            ts[t] = t;
            for (var i = 0; i < nSamples; i++)
                sliceBuffer[i] = samples[i].CenteredVolumeProfile[t];

            (meanProfile[t], stdProfile[t], semProfile[t]) = SeriesStatistics.ComputeSample<double>(sliceBuffer);
        }

        (var fitCurve, var bestWidth, var stalk, var rSquared) =
            DropletProfileFit.Fit(meanProfile, spacelikeDims);

        var lower1Sigma = new double[timeslices];
        var upper1Sigma = new double[timeslices];
        var maxTop = 1.0;

        for (var t = 0; t < timeslices; t++)
        {
            lower1Sigma[t] = Math.Max(0.0, meanProfile[t] - stdProfile[t]);
            upper1Sigma[t] = meanProfile[t] + stdProfile[t];
            maxTop = Math.Max(maxTop, Math.Max(upper1Sigma[t], fitCurve[t]));
        }

        var band = plt.Add.FillY(ts, lower1Sigma, upper1Sigma);
        band.FillColor = PlotPalette.Blue.WithAlpha(0.16f);
        band.LineWidth = 0;
        band.LegendText = "Single-Snapshot Fluctuation (±1σ)";

        var uniformCylinder = meanProfile.Average();
        var cylLine = plt.Add.HorizontalLine(uniformCylinder);
        cylLine.Color = PlotPalette.NeutralGray;
        cylLine.LineWidth = 1.6f;
        cylLine.LinePattern = LinePattern.Dotted;
        cylLine.LegendText = $"Uniform Cylinder Baseline ({uniformCylinder:F0}/slice)";

        var expLabel = FormatSuperscript(spacelikeDims);
        var fitPlot = plt.Add.Scatter(ts, fitCurve);
        fitPlot.Color = PlotPalette.Red;
        fitPlot.LineWidth = 2.6f;
        fitPlot.LinePattern = LinePattern.Dashed;
        fitPlot.MarkerSize = 0;
        fitPlot.LegendText = $"Spherical cos{expLabel} Fit (W={bestWidth:F1}, stalk={stalk:F1}, R²={rSquared:F4})";

        var errBar = plt.Add.ErrorBar(ts, meanProfile, semProfile);
        errBar.Color = PlotPalette.Blue;
        errBar.LineWidth = 1.5f;

        var meanScatter = plt.Add.Scatter(ts, meanProfile);
        meanScatter.Color = PlotPalette.Blue;
        meanScatter.LineWidth = 2.2f;
        meanScatter.MarkerSize = 7;
        meanScatter.MarkerShape = MarkerShape.FilledCircle;
        meanScatter.LegendText = $"Ensemble Mean <N(t)> ± SEM (n={nSamples})";

        plt.Axes.SetLimits(-0.5, timeslices - 0.5, 0.0, maxTop * 1.18);

        plt.Title($"Ensemble Proper-Time Volume Profile (T={timeslices}, R²={rSquared:F4})");
        plt.XLabel("Centered Proper Time Slice (t)");
        plt.YLabel("Simplex Volume N(t)");
        plt.ShowLegend(Alignment.UpperRight);

        return true;
    }
}