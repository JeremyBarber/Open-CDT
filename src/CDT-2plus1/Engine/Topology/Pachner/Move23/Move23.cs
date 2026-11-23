using CDT_2plus1.Engine.Topology.Pachner.Move23.Propose;
using CDT_Agnostic.Physics;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1.Engine.Topology.Pachner.Move23;

/// <summary>
/// Executes the <c>(2,3)</c> Pachner move for 2+1D Causal Dynamical Triangulations.
/// Replaces a timelike triangle shared by a <c>(2,2)</c> simplex and an asymmetric <c>(3,1)</c>/<c>(1,3)</c> simplex
/// with a new timelike edge surrounded by two <c>(2,2)</c> simplices and one asymmetric simplex (<c>ΔN_22 = +1</c>).
/// </summary>
internal readonly partial struct Move23
    : IPachnerMove<Context, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>
{
    /// <summary>Topological count changes produced by a <c>(2,3)</c> move (<c>ΔN_0 = 0, ΔN_22 = +1, ΔN_3 = +1</c>).</summary>
    internal static readonly PachnerDelta Deltas = new(dN0: 0, dN31: 0, dN22: 1, dN13: 0);

    /// <summary>Probability of selecting the target timelike facet out of the 4 facets of the chosen <c>(2,2)</c> simplex.</summary>
    internal const double ForwardProbability = 1.0 / 4.0;

    /// <summary>Probability of the inverse <c>(3,2)</c> move selecting the central timelike edge from an incident <c>(2,2)</c> simplex.</summary>
    internal const double ReverseProbability = Move32.Move32.ForwardProbability;

    /// <summary>Static proposal asymmetry factor (<c>P_rev / P_fwd</c>).</summary>
    private const double HastingsFactor = ReverseProbability / ForwardProbability;
}