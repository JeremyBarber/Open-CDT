using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;
using Open_CDT.Workflows.Plots;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.PhaseClassifier.Plots;

/// <summary>
/// Renders <c>phase_slice_continuity.png</c> tracking inter-slice volume smoothness <c>C_time</c>
/// across the <c>C ↔ A</c> foliation decoupling transition.
/// </summary>
internal sealed class PhaseSliceContinuityPlot : PlotBase<PhaseSpaceResult>
{
    private const double AxisMarginFraction = 0.04;

    /// <inheritdoc/>
    public override string FileName => "phase_slice_continuity.png";

    /// <inheritdoc/>
    public override string Description => "Foliation Continuity C_time";

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

        var lastCK0 = new List<double>();
        var firstAK0 = new List<double>();
        var lastCCt = new List<double>();
        var firstACt = new List<double>();

        for (var s = 0; s < shownSlices.Length; s++)
        {
            var slice = shownSlices[s].OrderBy(p => p.K0).ToArray();
            var color = shownSlices.Length == 1 ? PlotPalette.Blue : PlotPalette.Series(s);
            var deltaLabel = shownSlices.Length > 1 ? $" (Δ={slice[0].Delta:F2})" : "";

            var contLine = plt.Add.Scatter(
                slice.Select(p => p.K0).ToArray(),
                slice.Select(p => p.TemporalContinuity).ToArray());
            contLine.Color = color;
            contLine.LineWidth = 2.2f;
            contLine.MarkerSize = 6.5f;
            contLine.MarkerShape = MarkerShape.FilledTriangleUp;
            contLine.LegendText = $"Inter-Slice Smoothness C_time{deltaLabel}";

            if (PhaseDiagramPlot.TryGetClassifiedRibbonBounds(slice, out var idxC, out var idxA))
            {
                lastCK0.Add(slice[idxC].K0);
                firstAK0.Add(slice[idxA].K0);
                lastCCt.Add(slice[idxC].TemporalContinuity);
                firstACt.Add(slice[idxA].TemporalContinuity);

                if (shownSlices.Length == 1)
                {
                    var span = plt.Add.HorizontalSpan(slice[idxC].K0, slice[idxA].K0);
                    span.FillColor = PlotPalette.Red.WithAlpha(0.22f);
                    span.LegendText = $"C ↔ A Decoupling Ribbon [{slice[idxC].K0:F2} .. {slice[idxA].K0:F2}]";
                }
            }
        }

        if (shownSlices.Length > 1 && lastCK0.Count >= 2)
        {
            AddHorizontalBandPolygon(plt, lastCK0, lastCCt, firstAK0, firstACt, PlotPalette.Red, 0.24f, "C ↔ A Decoupling Ribbon");
        }

        var padK0 = (maxK0 - minK0) * AxisMarginFraction;
        var rawMinC = pts.Min(p => p.TemporalContinuity);
        var spanC = Math.Max(0.05, 1.0 - rawMinC);
        var yBottom = Math.Max(0.0, rawMinC - (spanC * 0.25));
        plt.Axes.SetLimits(minK0 - padK0, maxK0 + padK0, yBottom, 1.0 + (spanC * AxisMarginFraction));

        plt.Title(allSlices.Length > 1
            ? $"Foliation Slice Smoothness C_time ({shownSlices.Length} Δ Slices)"
            : "Foliation Slice Smoothness C_time Along Critical Surface");
        plt.XLabel("Bare Inverse Newton Constant (k0)");
        plt.YLabel("Inter-Slice Smoothness C_time [0 .. 1]");
        plt.ShowLegend(Alignment.LowerLeft);

        return true;
    }
}