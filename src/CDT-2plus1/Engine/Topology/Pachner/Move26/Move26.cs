using CDT_2plus1.Engine.Topology.Pachner.Move26.Propose;
using CDT_Agnostic.Physics;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1.Engine.Topology.Pachner.Move26;

/// <summary>
/// Executes the <c>(2,6)</c> Pachner move for 2+1D Causal Dynamical Triangulations.
/// Inserts a new vertex into the center of a spacelike triangle shared by a <c>(3,1)</c> and a <c>(1,3)</c> simplex,
/// expanding the local spatial geometry from 2 simplices up to 6 (<c>ΔN_0 = +1, ΔN_31 = +2, ΔN_13 = +2, ΔN_3 = +4</c>).
/// </summary>
internal readonly partial struct Move26
    : IPachnerMove<Context, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>
{
    internal static readonly PachnerDelta Deltas = new(dN0: 1, dN31: 2, dN22: 0, dN13: 2);

    // Theoretical probability of selecting this specific site out of the local neighborhood
    internal const double ForwardProbability = 1.0;

    // Theoretical probability of the reverse (6,2) move selecting this site to undo it
    internal const double ReverseProbability = Move62.Move62.ForwardProbability;

    // The asymmetric proposal ratio (P_rev / P_fwd)
    private const double HastingsFactor = ReverseProbability / ForwardProbability;
}