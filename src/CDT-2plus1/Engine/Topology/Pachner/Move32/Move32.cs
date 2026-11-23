using CDT_2plus1.Engine.Topology.Pachner.Move32.Propose;
using CDT_Agnostic.Physics;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1.Engine.Topology.Pachner.Move32;

/// <summary>
/// Executes the <c>(3,2)</c> Pachner move for 2+1D Causal Dynamical Triangulations.
/// Safely collapses a timelike edge shared by exactly 3 simplices (two <c>(2,2)</c>s and one asymmetric)
/// into a single timelike triangle, contracting the geometry from 3 simplices down to 2 (<c>ΔN_22 = -1</c>).
/// </summary>
internal readonly partial struct Move32
    : IPachnerMove<Context, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>
{
    internal static readonly PachnerDelta Deltas = new(dN0: 0, dN31: 0, dN22: -1, dN13: 0);

    // Theoretical probability of selecting this specific site out of the local neighborhood (2 of the 3 simplices are (2,2), and 1 of 6 edges is the central axis)
    internal const double ForwardProbability = 2.0 / 6.0;

    // Theoretical probability of the reverse (2,3) move selecting the shared face to undo it
    internal const double ReverseProbability = Move23.Move23.ForwardProbability;

    // The asymmetric proposal ratio (P_rev / P_fwd)
    private const double HastingsFactor = ReverseProbability / ForwardProbability;
}