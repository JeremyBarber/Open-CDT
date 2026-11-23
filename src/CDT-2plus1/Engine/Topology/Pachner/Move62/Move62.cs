using CDT_2plus1.Engine.Topology.Pachner.Move62.Propose;
using CDT_Agnostic.Physics;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1.Engine.Topology.Pachner.Move62;

/// <summary>
/// Executes the <c>(6,2)</c> Pachner move for 2+1D Causal Dynamical Triangulations.
/// Removes a degree-6 vertex from the spatial foliation by collapsing a bipyramid of 6 simplices
/// (three <c>(3,1)</c>s and three <c>(1,3)</c>s) down into a single <c>(3,1)</c>/<c>(1,3)</c> pair
/// (<c>ΔN_0 = -1, ΔN_31 = -2, ΔN_13 = -2, ΔN_3 = -4</c>).
/// </summary>
internal readonly partial struct Move62
    : IPachnerMove<Context, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>
{
    internal static readonly PachnerDelta Deltas = new(dN0: -1, dN31: -2, dN22: 0, dN13: -2);

    // Theoretical probability of selecting this specific site out of the local neighborhood
    internal const double ForwardProbability = 1.0;

    // Theoretical probability of the reverse (2,6) move selecting this site to undo it
    internal const double ReverseProbability = Move26.Move26.ForwardProbability;

    // The asymmetric proposal ratio (P_rev / P_fwd)
    private const double HastingsFactor = ReverseProbability / ForwardProbability;
}