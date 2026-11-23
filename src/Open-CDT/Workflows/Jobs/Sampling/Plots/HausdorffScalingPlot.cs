using Open_CDT.Statistics;
using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Sampling.Data;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.Sampling.Plots;

/// <summary>
/// Renders <c>hausdorff_scaling_loglog.png</c> displaying log-log geodesic ball <c>&lt;V(r)&gt;</c>
/// and shell <c>&lt;N(r)&gt;</c> scaling alongside the bulk power-law regression.
/// </summary>
internal sealed class HausdorffScalingPlot : PlotBase<SamplingResult>
{
    /// <inheritdoc/>
    public override string FileName => "hausdorff_scaling_loglog.png";

    /// <inheritdoc/>
    public override string Description => "Geodesic Hausdorff Ball Scaling";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, SamplingResult result)
    {
        var samples = result.Samples;
        if (samples.Count == 0)
            return false;

        var maxR = samples[0].Hausdorff.Profile.Length;
        if (maxR < 4)
            return false;

        var dSpace = result.SpacelikeDimensions;
        var dSpacetime = dSpace + 1;
        var sphereSup = FormatSuperscript(dSpace);

        var meanShell = new double[maxR];
        var meanBall = new double[maxR];

        for (var r = 0; r < maxR; r++)
        {
            var sum = 0.0;
            for (var i = 0; i < samples.Count; i++)
                sum += samples[i].Hausdorff.Profile[r];

            meanShell[r] = sum / samples.Count;
            meanBall[r] = r == 0 ? meanShell[0] : meanBall[r - 1] + meanShell[r];
        }

        var peakR = 2;
        for (var r = 2; r < maxR; r++)
        {
            if (meanShell[r] > meanShell[peakR])
                peakR = r;
        }

        var fitStart = Math.Max(3, peakR / 4);
        var fitEnd = Math.Min(maxR - 1, Math.Max(fitStart + 2, (int)(peakR * 0.75)));

        var logRList = new List<double>(maxR);
        var logShellList = new List<double>(maxR);
        var logBallList = new List<double>(maxR);

        for (var r = 1; r < maxR; r++)
        {
            if (meanShell[r] >= 1.0 && meanBall[r] > 0.0)
            {
                logRList.Add(Math.Log10(r));
                logShellList.Add(Math.Log10(meanShell[r]));
                logBallList.Add(Math.Log10(meanBall[r]));
            }
        }

        if (logRList.Count == 0)
            return false;

        var logR = logRList.ToArray();
        var logShell = logShellList.ToArray();
        var logBall = logBallList.ToArray();

        var fitX = new List<double>(fitEnd - fitStart + 1);
        var fitY = new List<double>(fitEnd - fitStart + 1);
        for (var r = fitStart; r <= fitEnd && r < maxR; r++)
        {
            if (meanBall[r] > 0.0)
            {
                fitX.Add(Math.Log10(r));
                fitY.Add(Math.Log10(meanBall[r]));
            }
        }

        (var slopeBall, var interceptBall) = SeriesStatistics.FitLinear(fitX.ToArray(), fitY.ToArray());

        var sampleDims = samples
            .Select(s => s.Hausdorff.Dimension)
            .Where(double.IsFinite)
            .ToArray();

        var meanSampleDh = slopeBall;
        var sdSampleDh = 0.0;
        if (sampleDims.Length > 0)
        {
            (meanSampleDh, sdSampleDh, _) = SeriesStatistics.ComputeSample<double>(sampleDims);
        }

        var xStart = Math.Log10(fitStart);
        var xEnd = Math.Log10(fitEnd);
        var windowSpan = plt.Add.HorizontalSpan(xStart, xEnd);
        windowSpan.FillColor = PlotPalette.Green.WithAlpha(0.14f);
        windowSpan.LegendText = $"{dSpacetime}D Bulk Scaling Window r in [{fitStart}, {fitEnd}] (r_peak={peakR})";

        var logPeakR = Math.Log10(peakR);
        var peakLine = plt.Add.VerticalLine(logPeakR);
        peakLine.Color = PlotPalette.Amber;
        peakLine.LineWidth = 2.0f;
        peakLine.LinePattern = LinePattern.Dotted;
        peakLine.LegendText = $"Spatial S{sphereSup} Saturation Peak (r_peak = {peakR}, log10 r = {logPeakR:F2})";

        var ballCurve = plt.Add.Scatter(logR, logBall);
        ballCurve.Color = PlotPalette.Blue;
        ballCurve.LineWidth = 2.2f;
        ballCurve.MarkerSize = 7;
        ballCurve.MarkerShape = MarkerShape.FilledCircle;
        ballCurve.LegendText = "Cumulative Ball Volume log10 <V(r)>";

        var shellCurve = plt.Add.Scatter(logR, logShell);
        shellCurve.Color = PlotPalette.Purple;
        shellCurve.LineWidth = 2.0f;
        shellCurve.MarkerSize = 6;
        shellCurve.MarkerShape = MarkerShape.FilledSquare;
        shellCurve.LegendText = "Concentric Shell Area log10 <N(r)>";

        AddLineSegment(
            plt,
            xStart,
            (slopeBall * xStart) + interceptBall,
            xEnd,
            (slopeBall * xEnd) + interceptBall,
            PlotPalette.Red,
            2.8f,
            LinePattern.Dashed,
            $"Power-Law Fit (Ball D_H = {slopeBall:F2}, Sample Mean D_H = {meanSampleDh:F3})");

        var minX = Math.Min(0.0, logR.Min());
        var maxX = Math.Max(xEnd, logR.Max());
        var padX = Math.Max(0.04, (maxX - minX) * 0.05);

        var minY = Math.Min(logShell.Min(), logBall.Min());
        var maxY = logBall.Max();
        var padY = Math.Max(0.10, (maxY - minY) * 0.08);

        plt.Axes.SetLimits(minX - padX, maxX + padX, minY - padY, maxY + (padY * 1.25));

        plt.Title($"Geodesic Hausdorff Scaling (Mean D_H = {meanSampleDh:F3} ± {sdSampleDh:F3}, Bulk Ball Fit = {slopeBall:F2})");
        plt.XLabel("log10(r) — Geodesic Dual-Graph Radius");
        plt.YLabel("log10(Simplex Count)");
        plt.ShowLegend(Alignment.UpperLeft);

        return true;
    }
}