using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Sampling.Data;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.Sampling.Plots;

/// <summary>
/// Renders <c>volume_profile_harmonics.png</c> displaying the relative circular Fourier power
/// spectrum across the lowest temporal modes.
/// </summary>
internal sealed class VolumeProfileHarmonicsPlot : PlotBase<SamplingResult>
{
    private const int MaxDisplayedModes = 5;

    /// <inheritdoc/>
    public override string FileName => "volume_profile_harmonics.png";

    /// <inheritdoc/>
    public override string Description => "Temporal Fourier Power Spectrum";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, SamplingResult result)
    {
        var samples = result.Samples;
        if (samples.Count == 0)
            return false;

        var timeslices = samples[0].CenteredVolumeProfile.Length;
        var modeCount = Math.Min(MaxDisplayedModes, timeslices / 2);
        if (modeCount == 0)
            return false;

        var modeShares = new double[modeCount];
        var power = new double[modeCount];

        foreach (var s in samples)
        {
            var v = s.CenteredVolumeProfile;
            var totalPower = 0.0;

            for (var k = 1; k <= modeCount; k++)
            {
                var re = 0.0;
                var im = 0.0;
                for (var t = 0; t < timeslices; t++)
                {
                    var angle = 2.0 * Math.PI * k * t / timeslices;
                    re += v[t] * Math.Cos(angle);
                    im += v[t] * Math.Sin(angle);
                }

                power[k - 1] = (re * re) + (im * im);
                totalPower += power[k - 1];
            }

            if (totalPower > 1e-12)
            {
                for (var k = 0; k < modeCount; k++)
                    modeShares[k] += (power[k] / totalPower) * 100.0;
            }
        }

        for (var k = 0; k < modeCount; k++)
            modeShares[k] /= samples.Count;

        var bars = new List<Bar>(modeCount);
        var ticks = new Tick[modeCount];

        for (var k = 0; k < modeCount; k++)
        {
            var color = k == 0 ? PlotPalette.Green : PlotPalette.Blue;
            bars.Add(new Bar
            {
                Position = k + 1,
                Value = modeShares[k],
                FillColor = color.WithAlpha(0.85f),
                LineColor = color,
                LineWidth = 1.5f
            });

            ticks[k] = new Tick(k + 1, k == 0 ? "k=1 Fundamental" : $"k={k + 1} Harmonic");
            AddPointLabel(plt, $"{modeShares[k]:F1}%", k + 1, modeShares[k], placeAbove: true, verticalOffset: 4f, fontSize: 11f);
        }

        plt.Add.Bars(bars);
        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
        plt.Axes.SetLimits(0.4, modeCount + 0.6, 0.0, 112.0);

        plt.Title($"Volume Profile Harmonic Spectrum (k=1 Share = {modeShares[0]:F1}%)");
        plt.XLabel("Temporal Fourier Mode (k)");
        plt.YLabel("Mean Relative Spectral Power (%)");

        return true;
    }
}