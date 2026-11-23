using CDT_Agnostic.Physics;
using CDT_Agnostic.Physics.Components;
using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1.Engine.Topology.Pachner.Move62;

internal readonly partial struct Move62
{
    // Bake the Q28 shifted numerator at compile time
    private const ulong HastingsNumerator_Q28 = (ulong)(HastingsFactor * MoveWeights.Q_ONE);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMoveEnergeticallyFavourable(RandomAccessor random, InspectingAccessor inspector, PhysicsAccessor physics)
    {
        var h_Q28 = CalculateHastingsRatioQ28(inspector.N13, inspector.Adjacency.TotalVertices);
        return Metropolis.ShouldAccept(random, inspector.Adjacency.TotalSimplices, h_Q28, in physics.Move62);
    }

    /// <summary>
    /// Exposed exclusively for pure-math unit tests.
    /// Computes the Hastings ratio H = (N0_initial / N13_final) * (P_rev / P_fwd) in Q28 fixed-point math.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint CalculateHastingsRatioQ28(int currentN13, int currentN0)
    {
        var scaledN0 = (ulong)currentN0 * HastingsNumerator_Q28;
        return (uint)(scaledN0 / (uint)(currentN13 + Deltas.DeltaN13));
    }
}