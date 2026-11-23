using CDT_Agnostic.Accessors;
using CDT_Agnostic.RuntimeMemory.Identifiers;

namespace CDT_Agnostic.Measurements.Observables;

/// <summary>
/// Represents the complete snapshot of a Hausdorff dimension measurement.
/// </summary>
/// <param name="Dimension">The fitted macroscopic Hausdorff dimension (<c>D_H</c>), or <see cref="double.NaN"/> if the scaling window contains fewer than 2 radii.</param>
/// <param name="Profile">The mean geodesic shell volume profile <c>N(r)</c> for <c>r in [0 .. MaxRadius]</c>, averaged over all sampled BFS origins.</param>
public readonly record struct HausdorffResult(
    double Dimension,
    double[] Profile);

/// <summary>
/// Estimates the macroscopic Hausdorff dimension (<c>D_H</c>) via Breadth-First Search (BFS) on the dual simplicial graph.
/// Measures concentric geodesic shell volumes <c>N(r)</c> around random root simplices and fits the log-log scaling exponent
/// of the cumulative ball volume <c>V(r) = sum_{k=0}^r N(k) ~ r^{D_H}</c> prior to spatial slice saturation.
/// </summary>
/// <param name="samplesPerMeasurement">The number of independent random BFS origins averaged per measurement snapshot.</param>
/// <param name="maxRadius">The maximum dual geodesic distance (<c>r_max</c>) explored from each origin.</param>
/// <param name="absoluteArenaCapacity">The maximum simplex slot capacity (<c>MaxSimplices</c>) of the underlying arena.</param>
public sealed class Hausdorff(
    int samplesPerMeasurement,
    int maxRadius,
    int absoluteArenaCapacity) : IManifoldObservable<HausdorffResult>
{
    private const int MinPeakSearchRadius = 2;
    private const int MinBulkFitRadius = 3;
    private const int BulkWindowStartDivisor = 4;
    private const double PeakCutoffFraction = 0.75;
    private const int MinRegressionPoints = 2;
    private const double CollinearityEpsilon = 1e-12;

    // ========================================================================
    // ZERO-ALLOCATION BFS BUFFERS
    // ========================================================================
    // Because the unmanaged SlotStore recycles simplex IDs without compacting the arena,
    // an active manifold may contain simplex IDs up to the high-water mark (bounded by MaxSimplices).
    // Sizing both flat arrays to absoluteArenaCapacity guarantees:
    //   1. Since each simplex is visited at most once per BFS, tail never exceeds absoluteArenaCapacity.
    //   2. _visited[nb.Value] is a direct O(1) lookup that can never throw IndexOutOfRangeException.
    //   3. Incrementing _bfsGeneration invalidates all prior visited marks in O(1) time without Array.Clear().
    private readonly int[] _queue = new int[absoluteArenaCapacity];
    private readonly int[] _visited = new int[absoluteArenaCapacity];
    private int _bfsGeneration;

    /// <inheritdoc/>
    public HausdorffResult Measure<TAdjacency, TInspector, TRandom>(TInspector inspector, TRandom random)
        where TAdjacency : unmanaged, IAdjacencyAccessor
        where TInspector : unmanaged, IInspectingAccessor<TAdjacency>
        where TRandom : unmanaged, IRandomAccessor
    {
        var currentProfile = new double[maxRadius + 1];

        if (inspector.Adjacency.TotalSimplices == 0)
            return new HausdorffResult(double.NaN, currentProfile);

        // ====================================================================
        // 1. MULTI-ORIGIN GEODESIC SHELL SAMPLING
        // ====================================================================
        for (var s = 0; s < samplesPerMeasurement; s++)
        {
            _bfsGeneration++;

            var startId = random.SelectRandomActiveSimplex();
            RunBfs(inspector, startId, currentProfile);
        }

        for (var r = 0; r <= maxRadius; r++)
            currentProfile[r] /= samplesPerMeasurement;

        // ====================================================================
        // 2. MACROSCOPIC SCALING EXPONENT EXTRACTION
        // ====================================================================
        var dimension = FitHausdorffDimension(currentProfile);

        return new HausdorffResult(dimension, currentProfile);
    }

    // ========================================================================
    // WAVEFRONT BREADTH-FIRST SEARCH
    // ========================================================================
    private void RunBfs<TInspector>(TInspector inspector, SimplexId startId, double[] shellAccumulator)
        where TInspector : unmanaged, IInspectingAccessor
    {
        var head = 0;
        var tail = 0;

        _queue[tail++] = startId.Value;
        _visited[startId.Value] = _bfsGeneration;

        // Process the flat queue in discrete breadth-first waves [head .. levelEnd).
        // At the start of each radius r, (tail - head) is the exact shell volume N(r).
        for (var r = 0; r <= maxRadius && head < tail; r++)
        {
            var levelEnd = tail;
            shellAccumulator[r] += levelEnd - head;

            // Frontier cutoff: once the outermost requested shell (r == maxRadius) is counted,
            // exit immediately without expanding its neighbors into shell (maxRadius + 1).
            if (r == maxRadius)
                break;

            for (; head < levelEnd; head++)
            {
                SimplexId id = new(_queue[head]);

                for (var f = 0; f < TInspector.FacetsPerSimplex; f++)
                {
                    var nb = inspector.GetNeighbor(id, f);

                    if (nb != SimplexId.Invalid && _visited[nb.Value] != _bfsGeneration)
                    {
                        _visited[nb.Value] = _bfsGeneration;
                        _queue[tail++] = nb.Value;
                    }
                }
            }
        }
    }

    // ========================================================================
    // CUMULATIVE BALL VOLUME LOG-LOG REGRESSION
    // ========================================================================
    private double FitHausdorffDimension(double[] meanNr)
    {
        // 1. Locate the spatial S^d saturation peak (rPeak) where shell area N(r) is maximal.
        //    We start at r = 2 because r = 0 (N = 1) and r = 1 (N <= FacetsPerSimplex) are fixed UV lattice steps.
        //    Beyond rPeak, the BFS wavefront has wrapped around the compact spatial sphere and only propagates 1D along time.
        var rPeak = MinPeakSearchRadius;
        var maxShell = 0.0;
        for (var r = MinPeakSearchRadius; r <= maxRadius; r++)
        {
            if (meanNr[r] > maxShell)
            {
                maxShell = meanNr[r];
                rPeak = r;
            }
        }

        // 2. Define the continuum bulk scaling window [rMin, rMax]:
        //    - rMin >= 3 skips the short-distance UV discretization artifacts (r = 0, 1, 2).
        //    - rMax = 0.75 * rPeak stops strictly before spatial finite-size curvature bends the shell growth downward.
        //    - Clamping rMax to maxRadius prevents out-of-bounds indexing when small test radii (maxRadius < 5) are used.
        var rMin = Math.Max(MinBulkFitRadius, rPeak / BulkWindowStartDivisor);
        var rMax = Math.Min(maxRadius, Math.Max(rMin + MinRegressionPoints, (int)(rPeak * PeakCutoffFraction)));

        // 3. Ordinary Least Squares (OLS) regression of ln(V(r)) vs. ln(r),
        //    where V(r) = sum_{k=0}^r N(k) is the cumulative geodesic ball volume (V(r) ~ r^{D_H}).
        double sumXY = 0, sumX = 0, sumY = 0, sumX2 = 0;
        double cumulativeBallVolume = 0;
        var count = 0;

        for (var r = 0; r <= rMax; r++)
        {
            cumulativeBallVolume += meanNr[r];

            if (r < rMin || cumulativeBallVolume <= 0)
                continue;

            var x = Math.Log(r);
            var y = Math.Log(cumulativeBallVolume);
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
            count++;
        }

        if (count < MinRegressionPoints)
            return double.NaN;

        var denom = (count * sumX2) - (sumX * sumX);
        return Math.Abs(denom) < CollinearityEpsilon ? double.NaN : ((count * sumXY) - (sumX * sumY)) / denom;
    }
}