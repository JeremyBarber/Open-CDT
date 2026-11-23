using CDT_Agnostic.Accessors;
using CDT_Agnostic.RuntimeMemory.Identifiers;

namespace CDT_Agnostic.Measurements.Observables;

/// <summary>
/// Represents the complete snapshot of a spectral dimension measurement.
/// </summary>
/// <param name="EstimatedDimension">The asymptotic spectral dimension (<c>D_S</c>) fitted over the macroscopic diffusion tail, or <see cref="double.NaN"/> if fewer than two valid points exist.</param>
/// <param name="ReturnProbabilities">The mean heat-kernel return probability curve <c>P(σ)</c> for diffusion steps <c>σ in [0 .. SigmaMax]</c>.</param>
public readonly record struct SpectralDimensionResult(
    double EstimatedDimension,
    double[] ReturnProbabilities);

/// <summary>
/// Evaluates heat-kernel return probabilities <c>P(σ) = K(x_0, x_0; σ)</c> and estimates the scale-dependent
/// spectral dimension <c>D_S(σ) = -2 d(ln P(σ)) / d(ln σ)</c> via deterministic diffusion on the dual simplicial graph.
/// Dimensionally agnostic: operates purely on generic dual-graph facet adjacency.
/// </summary>
/// <param name="samplesPerMeasurement">The number of independent random origin simplices averaged per measurement snapshot.</param>
/// <param name="sigmaMax">The maximum number of discrete diffusion steps (<c>σ_max</c>) evolved per origin.</param>
/// <param name="absoluteArenaCapacity">The maximum simplex slot capacity (<c>MaxSimplices</c>) of the underlying arena.</param>
public sealed class SpectralDimension(
    int samplesPerMeasurement,
    int sigmaMax,
    int absoluteArenaCapacity) : IManifoldObservable<SpectralDimensionResult>
{
    /// <summary>
    /// The diffusion weight (<c>χ = 0.8</c>) distributed across neighboring simplices each step,
    /// leaving <c>1 - χ = 0.2</c> on the current simplex to damp bipartite parity oscillations.
    /// </summary>
    public const double DiffusionChi = 0.8;

    private const double HoldWeight = 1.0 - DiffusionChi;
    private const int MinRegressionPoints = 2;
    private const double CollinearityEpsilon = 1e-12;

    // ========================================================================
    // PRE-ALLOCATED ARENA DIFFUSION BUFFERS
    // ========================================================================
    // Because SlotStore recycles simplex IDs without compacting the arena, active simplices can occupy
    // physical slots anywhere in [0 .. absoluteArenaCapacity - 1]. Pre-allocating both probability
    // buffers to absoluteArenaCapacity allows direct O(1) neighbor lookups by physical SimplexId,
    // while each diffusion step iterates strictly over the dense active table in SlotStore.
    private readonly double[] _bufferA = new double[absoluteArenaCapacity];
    private readonly double[] _bufferB = new double[absoluteArenaCapacity];

    /// <inheritdoc/>
    public SpectralDimensionResult Measure<TAdjacency, TInspector, TRandom>(TInspector inspector, TRandom random)
        where TAdjacency : unmanaged, IAdjacencyAccessor
        where TInspector : unmanaged, IInspectingAccessor<TAdjacency>
        where TRandom : unmanaged, IRandomAccessor
    {
        var returnProbabilities = new double[sigmaMax + 1];

        var totalSimplices = inspector.Adjacency.TotalSimplices;
        if (totalSimplices == 0)
            return new SpectralDimensionResult(double.NaN, returnProbabilities);

        // ====================================================================
        // 1. DETERMINISTIC DUAL-GRAPH HEAT-KERNEL DIFFUSION
        // ====================================================================
        returnProbabilities[0] = 1.0;

        var current = _bufferA;
        var next = _bufferB;
        var facetWeight = DiffusionChi / TInspector.FacetsPerSimplex;

        for (var s = 0; s < samplesPerMeasurement; s++)
        {
            var origin = random.SelectRandomActiveSimplex();

            // Set initial condition K(x, x_0; 0) = delta_{x, x_0} across active slots only.
            for (var i = 0; i < totalSimplices; i++)
            {
                var id = inspector.Adjacency.GetNthSimplexId(i);
                current[id.Value] = 0.0;
            }

            current[origin.Value] = 1.0;

            for (var sigma = 1; sigma <= sigmaMax; sigma++)
            {
                for (var i = 0; i < totalSimplices; i++)
                {
                    var id = inspector.Adjacency.GetNthSimplexId(i);
                    var neighborSum = 0.0;

                    for (var f = 0; f < TInspector.FacetsPerSimplex; f++)
                    {
                        var nb = inspector.GetNeighbor(id, f);

                        // On closed manifolds every facet is linked; reflecting Invalid back onto id
                        // preserves exact probability conservation on bounded test subgraphs.
                        var nbIndex = nb == SimplexId.Invalid ? id.Value : nb.Value;
                        neighborSum += current[nbIndex];
                    }

                    next[id.Value] = (HoldWeight * current[id.Value]) + (facetWeight * neighborSum);
                }

                (current, next) = (next, current);
                returnProbabilities[sigma] += current[origin.Value];
            }
        }

        for (var sigma = 1; sigma <= sigmaMax; sigma++)
        {
            returnProbabilities[sigma] /= samplesPerMeasurement;
        }

        // ====================================================================
        // 2. ASYMPTOTIC MACROSCOPIC DIMENSION FIT
        // ====================================================================
        var estimatedDimension = EstimateSpectralDimension(returnProbabilities);

        return new SpectralDimensionResult(estimatedDimension, returnProbabilities);
    }

    // ========================================================================
    // LOG-LOG DIFFUSION TAIL REGRESSION
    // ========================================================================
    private double EstimateSpectralDimension(double[] probabilities)
    {
        var start = Math.Max(1, sigmaMax / 2);
        var end = sigmaMax;

        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;
        var count = 0;

        for (var i = start; i <= end; i++)
        {
            if (probabilities[i] > 0)
            {
                var x = Math.Log(i);
                var y = Math.Log(probabilities[i]);
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
                count++;
            }
        }

        if (count < MinRegressionPoints)
            return double.NaN;

        var denominator = (count * sumX2) - (sumX * sumX);
        if (Math.Abs(denominator) < CollinearityEpsilon)
            return double.NaN;

        var slope = ((count * sumXY) - (sumX * sumY)) / denominator;

        // Since return probability scales as P(σ) ~ σ^{-D_S / 2}, the spectral dimension is D_S = -2 * d(ln P) / d(ln σ).
        return -2.0 * slope;
    }
}