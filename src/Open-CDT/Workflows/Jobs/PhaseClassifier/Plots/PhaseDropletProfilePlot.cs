using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Helpers;
using Open_CDT.Workflows.Plots;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.PhaseClassifier.Plots;

/// <summary>
/// Renders <c>phase_droplet_concentration.png</c> tracking the peak slice volume fraction <c>O_max</c>
/// along the critical surface to diagnose Phase B single-slice localization (<c>O_max &gt; 0.50</c>).
/// </summary>
internal sealed class PhaseDropletProfilePlot : PlotBase<PhaseSpaceResult>
{
    private const double AxisMarginFraction = 0.04;

    /// <inheritdoc/>
    public override string FileName => "phase_droplet_concentration.png";

    /// <inheritdoc/>
    public override string Description => "Order Parameter O_max (C↔B)";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, PhaseSpaceResult result)
    {
        var pts = result.Points;
        if (pts.Count < 2)
            return false;

        var minK0 = pts.Min(p => p.K0);
        var maxK0 = pts.Max(p => p.K0);
        if (maxK0 <= minK0)
            return false;

        var allSlices = pts
            .GroupBy(p => p.Delta)
            .OrderBy(g => g.Key)
            .ToArray();

        var shownSlices = SelectEvenlySpaced(allSlices);

        var uniformBaseline = 1.0 / Math.Max(1u, result.Timeslices);
        var baseLine = plt.Add.HorizontalLine(uniformBaseline);
        baseLine.Color = PlotPalette.NeutralGray;
        baseLine.LineWidth = 1.6f;
        baseLine.LinePattern = LinePattern.Dotted;
        baseLine.LegendText = $"Uniform Cylinder Baseline 1/T = {uniformBaseline:F3}";

        const double threshold = PhaseBoundaryClassifier.PhaseBSingleSliceThreshold;

        var phaseBBand = plt.Add.VerticalSpan(threshold, 1.0);
        phaseBBand.FillColor = PlotPalette.Crimson.WithAlpha(0.14f);
        phaseBBand.LegendText = $"Phase B Region (O_max > {threshold:F2})";

        var phaseBLine = plt.Add.HorizontalLine(threshold);
        phaseBLine.Color = PlotPalette.Crimson;
        phaseBLine.LineWidth = 2.0f;
        phaseBLine.LinePattern = LinePattern.Dashed;

        for (var s = 0; s < shownSlices.Length; s++)
        {
            var slice = shownSlices[s].OrderBy(p => p.K0).ToArray();
            var color = shownSlices.Length == 1 ? PlotPalette.Green : PlotPalette.Series(s);
            var deltaLabel = shownSlices.Length > 1 ? $" (Δ={slice[0].Delta:F2})" : "";

            var line = plt.Add.Scatter(
                slice.Select(p => p.K0).ToArray(),
                slice.Select(p => p.PeakSliceFraction).ToArray());
            line.Color = color;
            line.LineWidth = 2.0f;
            line.MarkerSize = 6.5f;
            line.MarkerShape = MarkerShape.FilledCircle;
            line.LegendText = $"Peak Slice Fraction O_max{deltaLabel}";
        }

        var padK0 = (maxK0 - minK0) * AxisMarginFraction;
        var maxPeak = Math.Max(threshold, pts.Max(p => p.PeakSliceFraction));
        var yTop = Math.Min(1.0, maxPeak * 1.20);
        plt.Axes.SetLimits(minK0 - padK0, maxK0 + padK0, 0.0, yTop);

        plt.Title(allSlices.Length > 1
            ? $"Primary C ↔ B Order Parameter O_max = max N(t)/N ({shownSlices.Length} Δ Slices)"
            : "Primary C ↔ B Order Parameter O_max = max N(t)/N Along Critical Surface");
        plt.XLabel("Bare Inverse Newton Constant (k0)");
        plt.YLabel("Peak Single-Slice Volume Fraction O_max");
        plt.ShowLegend(Alignment.UpperLeft);

        return true;
    }
}