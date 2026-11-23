using System.Runtime.CompilerServices;

namespace CDT_Agnostic.Physics.Components;

/// <summary>
/// Evaluates the exact floating-point Euclidean Boltzmann weight <c>exp(-ΔS)</c> for a proposed topological move.
/// </summary>
internal static class BoltzmannWeight
{
    /// <summary>
    /// Computes <c>exp(-(ΔS_Regge + ΔS_vol))</c> from the precomputed Regge and volume-fixing differentials.
    /// </summary>
    /// <param name="reggeDelta">The move's change in the bare Regge action (<c>ΔS_Regge</c>).</param>
    /// <param name="baseVolumeS">The precomputed base volume-fixing term <c>ε(ΔV)^2</c>.</param>
    /// <param name="driftFactor">The precomputed linear volume-drift slope <c>2εΔV</c>.</param>
    /// <param name="diff">The current signed volume drift (<c>V_current - V_target</c>).</param>
    /// <returns>The unclipped floating-point Boltzmann weight <c>exp(-ΔS)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Evaluate(double reggeDelta, double baseVolumeS, double driftFactor, int diff) =>
        Math.Exp(-(reggeDelta + VolumeFixing.Evaluate(baseVolumeS, driftFactor, diff)));
}