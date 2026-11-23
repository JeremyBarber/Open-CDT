using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;
using Open_CDT.Workflows.Plots;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Plots;

/// <summary>
/// Renders <c>tuning_critical_surface.png</c> displaying the calibrated critical surface <c>k_d^c(k_0, Delta)</c>,
/// its target tolerance band <c>±δk_d</c>, and equilibrium vertex density <c>&lt;N_0&gt; / &lt;N&gt;</c>.
/// </summary>
internal sealed class CriticalSurfacePlot : PlotBase<TuningResult>
{
    private const double AxisMarginFraction = 0.04;

    /// <inheritdoc/>
    public override string FileName => "tuning_critical_surface.png";

    /// <inheritdoc/>
    public override string Description => "Critical Stability Surface";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, TuningResult result)
    {
        var criticalPoints = result.CriticalPoints;
        if (criticalPoints.Count < 2)
            return false;

        var allDeltaSlices = criticalPoints
            .GroupBy(c => c.Delta)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(c => c.K0).ToArray())
            .Where(slice => slice.Length >= 2)
            .ToArray();

        if (allDeltaSlices.Length == 0)
            return false;

        var deltaSlices = SelectEvenlySpaced(allDeltaSlices);

        var minK0 = criticalPoints.Min(c => c.K0);
        var maxK0 = criticalPoints.Max(c => c.K0);
        var padK0 = (maxK0 - minK0) * AxisMarginFraction;
        var xLeft = minK0 - padK0;
        var xRight = maxK0 + padK0;

        var deltaKd = result.ToleranceDeltaKd;
        var minKd = criticalPoints.Min(c => c.KdCrit);
        var maxKd = criticalPoints.Max(c => c.KdCrit);
        var padKd = Math.Max(2.5 * deltaKd, (maxKd - minKd) * 0.08);
        var yBottom = minKd - padKd;
        var yTop = maxKd + padKd;

        if (deltaSlices.Length == 1)
        {
            var primarySlice = deltaSlices[0];
            var bandXs = primarySlice.Select(c => c.K0).ToArray();
            var bandUpper = primarySlice.Select(c => c.KdCrit + deltaKd).ToArray();
            var bandLower = primarySlice.Select(c => c.KdCrit - deltaKd).ToArray();

            AddVerticalBandPolygon(
                plt,
                bandXs,
                bandUpper,
                Enumerable.Repeat(yTop, bandXs.Length).ToArray(),
                PlotPalette.Blue,
                0.10f,
                "Undervolume Region (kd > kd_c: Volume Below Target)");

            AddVerticalBandPolygon(
                plt,
                bandXs,
                Enumerable.Repeat(yBottom, bandXs.Length).ToArray(),
                bandLower,
                PlotPalette.Amber,
                0.10f,
                "Overvolume Region (kd < kd_c: Volume Above Target)");
        }

        var tolPercent = result.ToleranceRatio * 100.0;

        for (var s = 0; s < deltaSlices.Length; s++)
        {
            var slice = deltaSlices[s];
            var color = PlotPalette.Series(s);

            var xs = slice.Select(c => c.K0).ToArray();
            var upper = slice.Select(c => c.KdCrit + deltaKd).ToArray();
            var lower = slice.Select(c => c.KdCrit - deltaKd).ToArray();
            var deltaLabel = deltaSlices.Length > 1 ? $" (Δ={slice[0].Delta:F2})" : "";

            AddVerticalBandPolygon(
                plt,
                xs,
                lower,
                upper,
                color,
                0.24f,
                deltaSlices.Length == 1
                    ? $"Target Volume Band (±{tolPercent:G3}% → ±{deltaKd:F4} kd)"
                    : $"Target Band ±{deltaKd:F3}{deltaLabel}");

            var line = plt.Add.Scatter(xs, slice.Select(c => c.KdCrit).ToArray());
            line.Color = color;
            line.LineWidth = 2.6f;
            line.MarkerSize = 8f;
            line.MarkerShape = MarkerShape.FilledCircle;
            line.LegendText = $"Critical Line kd_c(k0){deltaLabel}";
        }

        if (deltaSlices.Length == 1)
        {
            var primarySlice = deltaSlices[0];
            var k0s = primarySlice.Select(c => c.K0).ToArray();
            var densities = primarySlice.Select(c => c.VertexDensity).ToArray();

            var densityLine = plt.Add.Scatter(k0s, densities);
            densityLine.Axes.YAxis = plt.Axes.Right;
            densityLine.Color = PlotPalette.Purple;
            densityLine.LineWidth = 2.2f;
            densityLine.LinePattern = LinePattern.Dashed;
            densityLine.MarkerSize = 7f;
            densityLine.MarkerShape = MarkerShape.OpenSquare;
            densityLine.LegendText = "Vertices per Simplex <N0>/<N> (Right Axis)";

            plt.Axes.Right.Label.Text = "Equilibrium Vertices per Simplex <N0>/<N>";
            plt.Axes.Right.Label.ForeColor = PlotPalette.Purple;
            plt.Axes.Right.TickLabelStyle.ForeColor = PlotPalette.Purple;

            var minD = densities.Min();
            var maxD = densities.Max();
            var spanD = maxD > minD ? maxD - minD : Math.Max(1e-3, maxD * 0.1);
            plt.Axes.SetLimitsY(minD - (spanD * 0.35), maxD + (spanD * 0.45), plt.Axes.Right);
        }

        plt.Axes.SetLimitsX(xLeft, xRight);
        plt.Axes.SetLimitsY(yBottom, yTop);

        plt.Title($"CDT Critical Phase-Space Surface ({criticalPoints.Count} Grid Coordinates)");
        plt.XLabel("Bare Inverse Newton Constant (k0)");
        plt.YLabel("Critical Cosmological Constant (kd_c)");
        plt.ShowLegend(Alignment.UpperLeft);

        return true;
    }
}