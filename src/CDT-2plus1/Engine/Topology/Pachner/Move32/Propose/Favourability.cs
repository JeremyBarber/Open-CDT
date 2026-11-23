using CDT_Agnostic.Physics;
using CDT_Agnostic.Physics.Components;

using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1.Engine.Topology.Pachner.Move32;

internal readonly partial struct Move32
{
    // Bake the Q28 shifted numerator at compile time
    private const ulong HastingsNumerator_Q28 = (ulong)(HastingsFactor * MoveWeights.Q_ONE);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMoveEnergeticallyFavourable(
        RandomAccessor random,
        InspectingAccessor inspector,
        PhysicsAccessor physics)
    {
        var h_Q28 = CalculateHastingsRatioQ28(inspector.N22);
        return Metropolis.ShouldAccept(random, inspector.Adjacency.TotalSimplices, h_Q28, in physics.Move32);
    }

    /// <summary>
    /// Exposed exclusively for pure-math unit tests.
    /// Computes the Hastings ratio <c>H = (N22_final / N22_initial) * (P_rev / P_fwd)</c> in <c>Q4.28</c> fixed-point math.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint CalculateHastingsRatioQ28(int currentN22)
    {
        var scaledNumerator = (ulong)(currentN22 + Deltas.DeltaN22) * HastingsNumerator_Q28;
        return (uint)(scaledNumerator / (uint)currentN22);
    }
}