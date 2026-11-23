using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Helpers;
using Open_CDT.Workflows.Plots;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.PhaseClassifier.Plots;

/// <summary>
/// Renders <c>phase_vertex_density.png</c> tracking equilibrium vertex density <c>&lt;N_0&gt; / &lt;N&gt;</c>,
/// the Barnette topological ceiling <c>1 / (2 d_s)</c>, and the <c>C ↔ A</c> transition ribbon.
/// </summary>
internal sealed class PhaseVertexDensityPlot : PlotBase<PhaseSpaceResult>
{
    private const double AxisMarginFraction = 0.04;

    /// <inheritdoc/>
    public override string FileName => "phase_vertex_density.png";

    /// <inheritdoc/>
    public override string Description => "Order Parameter <N0/N> (C↔A)";

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

        (_, var polymerCeiling) = PhaseBoundaryClassifier.ComputeTopologicalDensityBounds(result.SpacelikeDimensions);
        var ceilingLine = plt.Add.HorizontalLine(polymerCeiling);
        ceilingLine.Color = PlotPalette.NeutralGray;
        ceilingLine.LineWidth = 1.6f;
        ceilingLine.LinePattern = LinePattern.Dotted;
        ceilingLine.LegendText = $"Topological Ceiling 1/(2d_s) = {polymerCeiling:F3}";

        var allSlices = pts
            .GroupBy(p => p.Delta)
            .OrderBy(g => g.Key)
            .ToArray();

        var shownSlices = SelectEvenlySpaced(allSlices);

        var lastCK0 = new List<double>();
        var firstAK0 = new List<double>();
        var lastCVd = new List<double>();
        var firstAVd = new List<double>();

        for (var s = 0; s < shownSlices.Length; s++)
        {
            var slice = shownSlices[s].OrderBy(p => p.K0).ToArray();
            var color = shownSlices.Length == 1 ? PlotPalette.Purple : PlotPalette.Series(s);
            var deltaLabel = shownSlices.Length > 1 ? $" (Δ={slice[0].Delta:F2})" : "";

            var line = plt.Add.Scatter(
                slice.Select(p => p.K0).ToArray(),
                slice.Select(p => p.VertexDensity).ToArray());
            line.Color = color;
            line.LineWidth = 2.2f;
            line.MarkerSize = 6.5f;
            line.MarkerShape = MarkerShape.FilledSquare;
            line.LegendText = $"Vertices per Simplex <N0>/<N>{deltaLabel}";

            if (PhaseDiagramPlot.TryGetClassifiedRibbonBounds(slice, out var idxC, out var idxA))
            {
                lastCK0.Add(slice[idxC].K0);
                firstAK0.Add(slice[idxA].K0);
                lastCVd.Add(slice[idxC].VertexDensity);
                firstAVd.Add(slice[idxA].VertexDensity);

                if (shownSlices.Length == 1)
                {
                    var span = plt.Add.HorizontalSpan(slice[idxC].K0, slice[idxA].K0);
                    span.FillColor = PlotPalette.Red.WithAlpha(0.22f);
                    span.LegendText = $"C ↔ A Transition Ribbon [{slice[idxC].K0:F2} .. {slice[idxA].K0:F2}]";
                }
            }
        }

        if (shownSlices.Length > 1 && lastCK0.Count >= 2)
        {
            AddHorizontalBandPolygon(plt, lastCK0, lastCVd, firstAK0, firstAVd, PlotPalette.Red, 0.24f, "C ↔ A Transition Ribbon");

            var cEdge = plt.Add.Scatter(lastCK0.ToArray(), lastCVd.ToArray());
            cEdge.Color = PlotPalette.Green;
            cEdge.LineWidth = 1.6f;
            cEdge.LinePattern = LinePattern.Dashed;
            cEdge.MarkerSize = 7f;
            cEdge.MarkerShape = MarkerShape.FilledCircle;

            var aEdge = plt.Add.Scatter(firstAK0.ToArray(), firstAVd.ToArray());
            aEdge.Color = PlotPalette.Orange;
            aEdge.LineWidth = 1.6f;
            aEdge.LinePattern = LinePattern.Dashed;
            aEdge.MarkerSize = 7f;
            aEdge.MarkerShape = MarkerShape.FilledDiamond;
        }

        var padK0 = (maxK0 - minK0) * AxisMarginFraction;
        var minD = pts.Min(p => p.VertexDensity);
        var maxD = Math.Max(polymerCeiling, pts.Max(p => p.VertexDensity));
        var spanD = maxD > minD ? maxD - minD : polymerCeiling * 0.10;

        plt.Axes.SetLimits(
            minK0 - padK0,
            maxK0 + padK0,
            Math.Max(0.0, minD - (spanD * 0.32)),
            maxD + (spanD * 0.10));

        plt.Title(allSlices.Length > 1
            ? $"Primary C ↔ A Order Parameter <N0>/<N> ({shownSlices.Length} Δ Slices)"
            : "Primary C ↔ A Order Parameter <N0>/<N> Along Critical Surface");
        plt.XLabel("Bare Inverse Newton Constant (k0)");
        plt.YLabel("Equilibrium Vertices per Simplex <N0>/<N>");
        plt.ShowLegend(Alignment.LowerRight);

        return true;
    }
}