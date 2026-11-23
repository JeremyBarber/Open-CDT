using Open_CDT.Statistics;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;

namespace Open_CDT.Workflows.Jobs.PhaseClassifier.Helpers;

/// <summary>
/// Classifies critical-surface coordinates along constant-<c>Delta</c> slices into
/// Phase A, Phase B, and Phase C using dimension-derived topological bounds and plateau-elbow detection.
/// </summary>
internal static class PhaseBoundaryClassifier
{
    /// <summary>
    /// Exact single-slice majority fraction (<c>O_max &gt; 1/2</c>) indicating that one proper-time slab
    /// contains more simplices than all remaining slabs combined (Phase B).
    /// </summary>
    public const double PhaseBSingleSliceThreshold = 0.5;

    /// <summary>
    /// Evaluates each constant-<c>Delta</c> slice in <paramref name="rawPoints"/> and returns a new list
    /// with <see cref="PhasePointRecord.Phase"/> assigned.
    /// </summary>
    /// <param name="rawPoints">Unclassified critical-surface measurements.</param>
    /// <param name="spacelikeDimensions">Spatial manifold dimension <c>d_s</c> (e.g., <c>2</c> for 2+1D, <c>3</c> for 3+1D).</param>
    /// <returns>Classified <see cref="PhasePointRecord"/> entries ordered by <c>Delta</c> then <c>k_0</c>.</returns>
    public static List<PhasePointRecord> ClassifyAllPoints(
        IReadOnlyList<PhasePointRecord> rawPoints,
        uint spacelikeDimensions)
    {
        ArgumentOutOfRangeException.ThrowIfZero(spacelikeDimensions);

        var updated = new List<PhasePointRecord>(rawPoints.Count);
        (var prismBulk, var polymerCeiling) = ComputeTopologicalDensityBounds(spacelikeDimensions);
        var bridgeDepletionMidpoint = 0.5 * (prismBulk + polymerCeiling);

        foreach (var deltaGroup in rawPoints.GroupBy(p => p.Delta).OrderBy(g => g.Key))
        {
            var row = deltaGroup.OrderBy(p => p.K0).ToArray();

            if (TryFindTransitionRibbon(row, spacelikeDimensions, out var lastCIndex, out _))
            {
                for (var i = 0; i < row.Length; i++)
                {
                    var phase = row[i].PeakSliceFraction > PhaseBSingleSliceThreshold
                        ? SpacetimePhase.PhaseB
                        : i <= lastCIndex
                            ? SpacetimePhase.PhaseC
                            : SpacetimePhase.PhaseA;

                    updated.Add(row[i] with { Phase = phase });
                }
            }
            else
            {
                var nonCrumpled = row.Where(p => p.PeakSliceFraction <= PhaseBSingleSliceThreshold).ToArray();
                var sigmaShot = nonCrumpled.Length > 0 ? ComputeVertexDensityShotNoise(nonCrumpled) : 0.0;
                var isSaturatedPlateau = IsSinglePhaseRowInPhaseA(
                    nonCrumpled,
                    polymerCeiling,
                    bridgeDepletionMidpoint,
                    sigmaShot);

                foreach (var p in row)
                {
                    var phase = p.PeakSliceFraction > PhaseBSingleSliceThreshold
                        ? SpacetimePhase.PhaseB
                        : isSaturatedPlateau || p.VertexDensity >= polymerCeiling - sigmaShot
                            ? SpacetimePhase.PhaseA
                            : SpacetimePhase.PhaseC;

                    updated.Add(p with { Phase = phase });
                }
            }
        }

        return updated;
    }

    /// <summary>
    /// Locates the <c>C ↔ A</c> transition elbow along a <c>k_0</c>-sorted slice where the rising
    /// <c>&lt;N_0&gt;/&lt;N_d&gt;</c> ramp saturates onto its bridge-depleted topological plateau.
    /// </summary>
    /// <param name="row">Critical-surface points along a single constant-<c>Delta</c> slice, sorted ascending by <c>k_0</c>.</param>
    /// <param name="spacelikeDimensions">Spatial manifold dimension <c>d_s</c>.</param>
    /// <param name="lastPhaseCIndex">Index in <paramref name="row"/> of the final Phase C point preceding the elbow, or <c>-1</c>.</param>
    /// <param name="firstPhaseAIndex">Index in <paramref name="row"/> of the first Phase A plateau point at the elbow, or <c>-1</c>.</param>
    /// <returns><c>true</c> if a genuine ramp-to-plateau transition exists within <paramref name="row"/>; otherwise <c>false</c>.</returns>
    public static bool TryFindTransitionRibbon(
        PhasePointRecord[] row,
        uint spacelikeDimensions,
        out int lastPhaseCIndex,
        out int firstPhaseAIndex)
    {
        lastPhaseCIndex = -1;
        firstPhaseAIndex = -1;

        // Isolate non-Phase-B points so collapsed low-k0 pancakes do not distort the C <-> A chord anchor.
        var nonCrumpledIndices = new List<int>(row.Length);
        for (var i = 0; i < row.Length; i++)
        {
            if (row[i].PeakSliceFraction <= PhaseBSingleSliceThreshold)
                nonCrumpledIndices.Add(i);
        }

        var m = nonCrumpledIndices.Count;
        if (m < 3)
            return false;

        var subRow = new PhasePointRecord[m];
        for (var i = 0; i < m; i++)
            subRow[i] = row[nonCrumpledIndices[i]];

        var sigmaShot = ComputeVertexDensityShotNoise(subRow);
        var rmseLinear = ComputeLinearFitRmse(subRow);

        // If a single unbent line already explains the extended scan within 1σ Poisson counting noise, no elbow exists.
        if (rmseLinear <= sigmaShot)
            return false;

        // Maximize Φ(j) = (ρ_j - ρ_0)² / (k0_j - k0_0) across [1 .. m - 1].
        var k0Start = subRow[0].K0;
        var rhoStart = subRow[0].VertexDensity;
        var bestSubIndex = -1;
        var maxChordRate = 0.0;

        for (var j = 1; j < m; j++)
        {
            var dk = subRow[j].K0 - k0Start;
            var dRho = subRow[j].VertexDensity - rhoStart;
            if (dk <= 0.0 || dRho <= 0.0)
                continue;

            var chordRate = (dRho * dRho) / dk;
            if (chordRate > maxChordRate)
            {
                maxChordRate = chordRate;
                bestSubIndex = j;
            }
        }

        if (bestSubIndex < 1)
            return false;

        (var prismBulk, var polymerCeiling) = ComputeTopologicalDensityBounds(spacelikeDimensions);
        var bridgeDepletionMidpoint = 0.5 * (prismBulk + polymerCeiling);

        // If Φ peaks on the final grid point, accept only when that point has already slammed into the Barnette ceiling.
        if (bestSubIndex == m - 1)
        {
            var ceilingFloor = polymerCeiling - sigmaShot;
            if (subRow[m - 1].VertexDensity >= ceilingFloor && subRow[m - 2].VertexDensity < ceilingFloor)
            {
                lastPhaseCIndex = nonCrumpledIndices[m - 2];
                firstPhaseAIndex = nonCrumpledIndices[m - 1];
                return true;
            }

            return false;
        }

        var plateauDensities = new double[m - bestSubIndex];
        for (var i = bestSubIndex; i < m; i++)
            plateauDensities[i - bestSubIndex] = subRow[i].VertexDensity;

        (var plateauMean, var plateauStdDev) = SeriesStatistics.ComputePopulation<double>(plateauDensities);
        if (plateauMean <= bridgeDepletionMidpoint || plateauStdDev >= rmseLinear)
            return false;

        lastPhaseCIndex = nonCrumpledIndices[bestSubIndex - 1];
        firstPhaseAIndex = nonCrumpledIndices[bestSubIndex];
        return true;
    }

    /// <summary>
    /// Returns the uniform causal prism density <c>1 / (d_s (d_s + 1))</c> and the Barnette
    /// stacked-sphere ceiling <c>1 / (2 d_s)</c> for spatial dimension <paramref name="spacelikeDimensions"/>.
    /// </summary>
    public static (double PrismBulk, double PolymerCeiling) ComputeTopologicalDensityBounds(uint spacelikeDimensions)
    {
        double ds = spacelikeDimensions;
        return (1.0 / (ds * (ds + 1.0)), 1.0 / (2.0 * ds));
    }

    private static bool IsSinglePhaseRowInPhaseA(
        PhasePointRecord[] row,
        double polymerCeiling,
        double bridgeDepletionMidpoint,
        double sigmaShot)
    {
        if (row.Length == 0)
            return false;

        if (row.Length == 1)
            return row[0].VertexDensity >= polymerCeiling - sigmaShot;

        var meanDensity = row.Average(p => p.VertexDensity);
        var endpointDrift = row[^1].VertexDensity - row[0].VertexDensity;

        return meanDensity > bridgeDepletionMidpoint
            && endpointDrift <= Math.Sqrt(2.0) * sigmaShot;
    }

    private static double ComputeVertexDensityShotNoise(PhasePointRecord[] row)
    {
        var meanDensity = row.Average(p => p.VertexDensity);
        var meanVolume = Math.Max(1.0, row.Average(p => p.MeanVolume));
        return Math.Sqrt(Math.Max(0.0, meanDensity) / meanVolume);
    }

    private static double ComputeLinearFitRmse(PhasePointRecord[] row)
    {
        var n = row.Length;
        var xs = new double[n];
        var ys = new double[n];

        for (var i = 0; i < n; i++)
        {
            xs[i] = row[i].K0;
            ys[i] = row[i].VertexDensity;
        }

        (var slope, var intercept) = SeriesStatistics.FitLinear(xs, ys);
        var sumSq = 0.0;
        for (var i = 0; i < n; i++)
        {
            var err = ys[i] - ((slope * xs[i]) + intercept);
            sumSq += err * err;
        }

        return Math.Sqrt(sumSq / n);
    }
}