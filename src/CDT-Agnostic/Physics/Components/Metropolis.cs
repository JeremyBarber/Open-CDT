using CDT_Agnostic.Accessors;

using System.Runtime.CompilerServices;

namespace CDT_Agnostic.Physics.Components;

/// <summary>
/// Universal, dimension-agnostic Metropolis-Hastings acceptance evaluator for topological transitions.
/// Combines the combinatorial Hastings proposal ratio (<c>h</c>) with the thermodynamic Boltzmann weight (<c>exp(-ΔS)</c>).
/// </summary>
public static class Metropolis
{
    private const double UIntToDouble = 1.0 / (1UL << 32); // Exact 2^-32
    private const int EntropyShift = (sizeof(uint) * 8) - MoveWeights.Q_SHIFT;

    /// <summary>
    /// Determines whether a proposed Pachner move is accepted under the Metropolis-Hastings criterion
    /// <c>P_accept = min(1, h * exp(-ΔS))</c>.
    /// </summary>
    /// <typeparam name="TRandom">The concrete unmanaged <see cref="IRandomAccessor"/> implementation.</typeparam>
    /// <param name="random">The unmanaged random number generator lens.</param>
    /// <param name="currentVolume">The current number of top-dimensional simplices in the manifold (<c>V_current</c>).</param>
    /// <param name="h_Q28">The combinatorial Hastings proposal ratio encoded in unsigned <c>Q4.28</c> fixed-point format.</param>
    /// <param name="weights">Read-only reference to the precomputed <see cref="MoveWeights"/> lookup table for the move.</param>
    /// <returns><see langword="true"/> if the move is accepted; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldAccept<TRandom>(
        TRandom random,
        int currentVolume,
        uint h_Q28,
        in MoveWeights weights)
        where TRandom : unmanaged, IRandomAccessor
    {
        var diff = currentVolume - weights.VolumeTarget;

        // ====================================================================
        // FAST PATH: L1 Cache Lookup + Branchless Q4.28 Integer ALU Math
        // ====================================================================
        // Unsigned range check verifies (-VolumeRadius <= diff <= +VolumeRadius) in a single
        // comparison without invoking Math.Abs (which emits an OverflowException check for int.MinValue).
        if ((uint)(diff + weights.VolumeRadius) <= (uint)(weights.VolumeRadius * 2))
        {
            var e_Q28 = weights.GetExpDeltaS(diff);

            // Widen to 64-bit ulong before multiplying two Q4.28 values so (h_Q28 * e_Q28) can never overflow.
            var pAccept_Q28 = ((ulong)h_Q28 * e_Q28) >> MoveWeights.Q_SHIFT;

            // Draw 28 bits of uniform entropy in [0, Q_ONE - 1].
            // If pAccept >= 1.0, then pAccept_Q28 >= Q_ONE, so (entropy < pAccept_Q28) is guaranteed true
            // without needing a separate branch for pAccept >= 1.0.
            var entropy = random.GetRandom() >> EntropyShift;

            return entropy < pAccept_Q28;
        }

        // ====================================================================
        // SLOW PATH: Double-Precision FPU Fallback (Extreme Volume Excursions)
        // ====================================================================
        return EvaluateSlowPath(random, diff, h_Q28, in weights);
    }

    /// <summary>
    /// Evaluates the exact double-precision Metropolis-Hastings criterion when the manifold volume
    /// drifts outside the precomputed lookup table radius (e.g., during initial cold-start thermalization).
    /// Marked <see cref="MethodImplOptions.NoInlining"/> to keep FPU instructions out of the hot loop's instruction cache.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool EvaluateSlowPath<TRandom>(
        TRandom random,
        int diff,
        uint h_Q28,
        in MoveWeights weights)
        where TRandom : unmanaged, IRandomAccessor
    {
        // 1. Recover the fractional Hastings proposal ratio from Q4.28 fixed-point
        var hastingsRatio = (double)h_Q28 / MoveWeights.Q_ONE;

        // 2. Compute the exact floating-point Boltzmann weight exp(-ΔS)
        var e = BoltzmannWeight.Evaluate(weights.ReggeDelta, weights.BaseVolumeS, weights.DriftFactor, diff);

        var acceptance = hastingsRatio * e;

        if (acceptance >= 1.0)
            return true;

        // 3. Draw a uniform double in [0, 1) from 32-bit entropy
        return (random.GetRandom() * UIntToDouble) < acceptance;
    }
}