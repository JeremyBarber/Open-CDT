using CDT_Agnostic.Physics;
using CDT_Agnostic.Physics.Components;

using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1.Engine.Topology.Pachner.Move23;

internal readonly partial struct Move23
{
    private const ulong HastingsNumerator_Q28 = (ulong)(HastingsFactor * MoveWeights.Q_ONE);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMoveEnergeticallyFavourable(
        RandomAccessor random,
        InspectingAccessor inspector,
        PhysicsAccessor physics)
    {
        var h_Q28 = CalculateHastingsRatioQ28(inspector.N22);
        return Metropolis.ShouldAccept(random, inspector.Adjacency.TotalSimplices, h_Q28, in physics.Move23);
    }

    /// <summary>
    /// Computes the <c>(2,3)</c> Hastings proposal ratio <c>H = (N_22 / (N_22 + 1)) * (P_rev / P_fwd)</c>
    /// in unsigned <c>Q4.28</c> fixed-point format.
    /// </summary>
    /// <param name="currentN22">The current number of active <c>(2,2)</c> simplices (<c>N_22</c>).</param>
    /// <returns>The <c>Q4.28</c> fixed-point Hastings ratio.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint CalculateHastingsRatioQ28(int currentN22)
    {
        var scaledN22 = (ulong)currentN22 * HastingsNumerator_Q28;
        return (uint)(scaledN22 / (uint)(currentN22 + Deltas.DeltaN22));
    }
}