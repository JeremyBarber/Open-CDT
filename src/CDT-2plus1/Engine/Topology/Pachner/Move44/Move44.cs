using CDT_2plus1.Engine.Topology.Pachner.Move44.Propose;
using CDT_Agnostic.Physics;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1.Engine.Topology.Pachner.Move44;

/// <summary>
/// Executes the <c>(4,4)</c> Pachner move for 2+1D Causal Dynamical Triangulations.
/// Flips a spatial edge shared by exactly four simplices (two <c>(3,1)</c>s and two <c>(1,3)</c>s).
/// This move is purely spatial and strictly preserves all simplex and vertex counts (<c>ΔN = 0</c>).
/// </summary>
internal readonly partial struct Move44
    : IPachnerMove<Context, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>
{
    internal static readonly PachnerDelta Deltas = new(dN0: 0, dN31: 0, dN22: 0, dN13: 0);

    // Theoretical probability of selecting this specific site out of the local neighborhood
    internal const double ForwardProbability = 4.0 / 6.0;

    // Theoretical probability of the reverse (4,4) move selecting this site to undo it
    internal const double ReverseProbability = ForwardProbability;

    // The symmetric proposal ratio (P_rev / P_fwd = 1.0)
    private const double HastingsFactor = ReverseProbability / ForwardProbability;
}