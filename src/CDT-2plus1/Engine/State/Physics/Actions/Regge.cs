using System.Runtime.CompilerServices;

using CDT_2plus1.Engine.Topology.Pachner;

namespace CDT_2plus1.Engine.State.Physics.Actions;

/// <summary>
/// Pure mathematical implementation of the Wick-rotated 2+1D Euclidean Regge Action differential.
/// Evaluated during setup and parameter tuning to populate the <c>Q4.28</c> thermodynamic Look-Up Tables.
/// </summary>
internal static class Regge
{
    /// <summary>
    /// Computes the exact change in the bare 2+1D Euclidean Regge action (<c>ΔS_Regge</c>) for a Pachner move:
    /// <para>
    /// <c>ΔS_Regge = -k_0 ΔN_0 + k_3 ΔN_3 + Δ (ΔN_31 + ΔN_13)</c>
    /// </para>
    /// </summary>
    /// <param name="constraints">The current bare coupling parameters (<c>k_0</c>, <c>k_3</c>, <c>Δ</c>) of the universe.</param>
    /// <param name="deltas">The discrete topological simplex and vertex count changes produced by the move.</param>
    /// <returns>The bare Regge action differential (<c>ΔS_Regge</c>).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateDeltaAction(in Constraints constraints, in PachnerDelta deltas) =>
        -(constraints.K0 * deltas.DeltaN0)
        + (constraints.K3 * deltas.DeltaN3)
        + (constraints.Delta * (deltas.DeltaN31 + deltas.DeltaN13));
}