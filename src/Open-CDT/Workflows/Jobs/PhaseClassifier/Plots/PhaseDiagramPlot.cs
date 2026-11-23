using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Helpers;
using Open_CDT.Workflows.Plots;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.PhaseClassifier.Plots;

/// <summary>
/// Renders <c>phase_space_diagram.png</c> across a 2D <c>(k_0, Delta)</c> critical grid,
/// shading Phase C/A regions, the <c>C ↔ A</c> transition ribbon, Phase B coordinates, and the Shoelace centroid target.
/// </summary>
internal sealed class PhaseDiagramPlot : PlotBase<PhaseSpaceResult>
{
    /// <inheritdoc/>
    public override string FileName => "phase_space_diagram.png";

    /// <inheritdoc/>
    public override string Description => "CDT Phase Space Diagram";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, PhaseSpaceResult result)
    {
        var pts = result.Points;
        if (pts.Count == 0)
            return false;

        var uniqueK0s = pts.Select(p => p.K0).Distinct().OrderBy(x => x).ToArray();
        var uniqueDeltas = pts.Select(p => p.Delta).Distinct().OrderBy(x => x).ToArray();

        if (uniqueK0s.Length < 2 || uniqueDeltas.Length < 2)
            return false;

        var dx = (uniqueK0s[^1] - uniqueK0s[0]) / (uniqueK0s.Length - 1);
        var xLeft = uniqueK0s[0];
        var xRight = uniqueK0s[^1];
        var yBottom = uniqueDeltas[0];
        var yTop = uniqueDeltas[^1];

        var lastCK0 = new List<double>();
        var firstAK0 = new List<double>();
        var ribbonDeltas = new List<double>();

        foreach (var deltaGroup in pts.GroupBy(p => p.Delta).OrderBy(g => g.Key))
        {
            var row = deltaGroup.OrderBy(p => p.K0).ToArray();
            if (TryGetClassifiedRibbonBounds(row, out var idxC, out var idxA))
            {
                lastCK0.Add(row[idxC].K0);
                firstAK0.Add(row[idxA].K0);
                ribbonDeltas.Add(row[0].Delta);
            }
        }

        var cCount = pts.Count(p => p.Phase == SpacetimePhase.PhaseC);
        var aCount = pts.Count(p => p.Phase == SpacetimePhase.PhaseA);
        var bCount = pts.Count(p => p.Phase == SpacetimePhase.PhaseB);
        var bPlottedAsUniformRegion = false;

        if (ribbonDeltas.Count >= 2)
        {
            var polyYs = ribbonDeltas.ToArray();
            var boundCXs = lastCK0.ToArray();
            var boundAXs = firstAK0.ToArray();
            var leftEdge = Enumerable.Repeat(xLeft, polyYs.Length).ToArray();
            var rightEdge = Enumerable.Repeat(xRight, polyYs.Length).ToArray();

            AddHorizontalBandPolygon(plt, leftEdge, polyYs, boundCXs, polyYs, PlotPalette.Green, 0.20f, $"Phase C (n={cCount})");
            AddHorizontalBandPolygon(plt, boundCXs, polyYs, boundAXs, polyYs, PlotPalette.Red, 0.28f, $"C ↔ A Transition Ribbon (Δk0 = {dx:F2})");
            AddHorizontalBandPolygon(plt, boundAXs, polyYs, rightEdge, polyYs, PlotPalette.Orange, 0.20f, $"Phase A (n={aCount})");

            var lineC = plt.Add.Scatter(boundCXs, polyYs);
            lineC.Color = PlotPalette.Green;
            lineC.LineWidth = 2.2f;
            lineC.LinePattern = LinePattern.Dashed;
            lineC.MarkerSize = 0;

            var lineA = plt.Add.Scatter(boundAXs, polyYs);
            lineA.Color = PlotPalette.Orange;
            lineA.LineWidth = 2.2f;
            lineA.LinePattern = LinePattern.Dashed;
            lineA.MarkerSize = 0;
        }
        else if (cCount == pts.Count || aCount == pts.Count || bCount == pts.Count)
        {
            var uniformPhase = pts[0].Phase;
            bPlottedAsUniformRegion = uniformPhase == SpacetimePhase.PhaseB;

            var (color, label) = uniformPhase switch
            {
                SpacetimePhase.PhaseC => (PlotPalette.Green, $"Phase C (n={cCount})"),
                SpacetimePhase.PhaseA => (PlotPalette.Orange, $"Phase A (n={aCount})"),
                _ => (PlotPalette.Crimson, $"Phase B (n={bCount})")
            };

            double[] ys = [yBottom, yTop];
            double[] leftXs = [xLeft, xLeft];
            double[] rightXs = [xRight, xRight];
            AddHorizontalBandPolygon(plt, leftXs, ys, rightXs, ys, color, 0.20f, label);
        }
        else
        {
            PlotPhasePoints(plt, pts, SpacetimePhase.PhaseC, PlotPalette.Green, MarkerShape.FilledCircle, "Phase C");
            PlotPhasePoints(plt, pts, SpacetimePhase.PhaseA, PlotPalette.Orange, MarkerShape.FilledDiamond, "Phase A");
        }

        if (bCount > 0 && !bPlottedAsUniformRegion)
        {
            PlotPhasePoints(plt, pts, SpacetimePhase.PhaseB, PlotPalette.Crimson, MarkerShape.FilledSquare, "Phase B");
        }

        if (PhaseCentroid.SelectTarget(pts) is PhasePointRecord rec)
        {
            AddSingleMarker(plt, rec.K0, rec.Delta, PlotPalette.Green, MarkerShape.FilledCircle, 10f);
            AddSingleMarker(
                plt,
                rec.K0,
                rec.Delta,
                PlotPalette.Slate,
                MarkerShape.OpenCircle,
                16f,
                $"Centroid Target (k0={rec.K0:F2}, Δ={rec.Delta:F2}, kd={rec.KdCrit:F5})");
        }

        plt.Axes.SetLimits(xLeft, xRight, yBottom, yTop);

        plt.Title($"2D CDT Phase Space Diagram ({pts.Count} Critical Coordinates, Target N={result.TargetVolume:N0})");
        plt.XLabel("Bare Inverse Newton Constant (k0)");
        plt.YLabel("Spacetime Asymmetry Parameter (Δ)");
        plt.ShowLegend(Alignment.UpperLeft);

        return true;
    }

    internal static bool TryGetClassifiedRibbonBounds(
        PhasePointRecord[] row,
        out int lastPhaseCIndex,
        out int firstPhaseAIndex)
    {
        for (var i = 0; i < row.Length - 1; i++)
        {
            if (row[i].Phase == SpacetimePhase.PhaseC &&
                row[i + 1].Phase == SpacetimePhase.PhaseA)
            {
                lastPhaseCIndex = i;
                firstPhaseAIndex = i + 1;
                return true;
            }
        }

        lastPhaseCIndex = -1;
        firstPhaseAIndex = -1;
        return false;
    }

    private static void PlotPhasePoints(
        Plot plt,
        IReadOnlyList<PhasePointRecord> pts,
        SpacetimePhase phase,
        Color color,
        MarkerShape shape,
        string label)
    {
        var group = pts.Where(p => p.Phase == phase).ToArray();
        if (group.Length == 0)
            return;

        var scatter = plt.Add.Scatter(
            group.Select(p => p.K0).ToArray(),
            group.Select(p => p.Delta).ToArray());
        scatter.Color = color;
        scatter.LineWidth = 0;
        scatter.MarkerSize = 9f;
        scatter.MarkerShape = shape;
        scatter.LegendText = $"{label} (n={group.Length})";
    }
}