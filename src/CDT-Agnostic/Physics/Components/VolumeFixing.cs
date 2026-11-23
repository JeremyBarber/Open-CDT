using System.Runtime.CompilerServices;

namespace CDT_Agnostic.Physics.Components;

/// <summary>
/// Evaluates the dimension-agnostic quadratic volume-fixing action differential
/// <c>ΔS_vol = ε(V + ΔV - V_target)^2 - ε(V - V_target)^2</c>.
/// </summary>
/// <remarks>
/// Expanding the binomial in terms of the current volume drift <c>D = V - V_target</c>
/// cancels the quadratic <c>ε D^2</c> term and reduces the differential to a linear function:
/// <c>ΔS_vol(D) = ε(ΔV)^2 + (2εΔV) * D</c>.
/// </remarks>
internal static class VolumeFixing
{
    /// <summary>
    /// Precomputes the static volume-fixing coefficients for a topological move that changes
    /// the top-dimensional simplex count by <paramref name="deltaVolume"/>.
    /// </summary>
    /// <param name="epsilon">The quadratic volume-fixing coupling strength (<c>ε</c>).</param>
    /// <param name="deltaVolume">The discrete volume change (<c>ΔV</c>) induced by the move.</param>
    /// <returns>
    /// A tuple containing <c>baseVolumeS = ε(ΔV)^2</c> (the action cost at <c>V == V_target</c>)
    /// and <c>driftFactor = 2εΔV</c> (the linear restoring slope per unit of volume drift).
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (double baseVolumeS, double driftFactor) CalculateConstants(double epsilon, int deltaVolume)
    {
        if (epsilon == 0.0 || deltaVolume == 0)
            return (0.0, 0.0);

        var baseVolumeS = epsilon * deltaVolume * deltaVolume;
        var driftFactor = 2.0 * epsilon * deltaVolume;

        return (baseVolumeS, driftFactor);
    }

    /// <summary>
    /// Evaluates the volume-fixing action differential <c>ΔS_vol</c> at a given <paramref name="volumeDrift"/>.
    /// </summary>
    /// <param name="baseVolumeS">The precomputed base volume action <c>ε(ΔV)^2</c>.</param>
    /// <param name="driftFactor">The precomputed linear drift slope <c>2εΔV</c>.</param>
    /// <param name="volumeDrift">The signed deviation from target volume (<c>V_current - V_target</c>).</param>
    /// <returns>The exact volume-fixing contribution to <c>ΔS</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Evaluate(double baseVolumeS, double driftFactor, int volumeDrift) =>
        baseVolumeS + (driftFactor * volumeDrift);
}