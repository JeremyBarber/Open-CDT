using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_2plus1.Engine.Topology.Pachner.Move62.Propose;

/// <summary>
/// The isolated geometric state required to safely execute a <c>(6,2)</c> Pachner move.
/// In 2+1D CDT, this move contracts a targeted spatial vertex entirely out of the manifold,
/// collapsing three <c>(3,1)</c> and three <c>(1,3)</c> simplices back into a single <c>(3,1)</c>/<c>(1,3)</c> pair.
/// </summary>
/// <param name="vTarget">The central spatial vertex targeted for deletion.</param>
/// <param name="u">The upper pole vertex of the cluster.</param>
/// <param name="v">The lower pole vertex of the cluster.</param>
/// <param name="a">The first equatorial vertex in the spatial plane.</param>
/// <param name="b">The second equatorial vertex in the spatial plane.</param>
/// <param name="c">The third equatorial vertex in the spatial plane.</param>
/// <param name="id31_0">The first <c>(3,1)</c> simplex in the upper hemisphere.</param>
/// <param name="id31_1">The second <c>(3,1)</c> simplex in the upper hemisphere.</param>
/// <param name="id31_2">The third <c>(3,1)</c> simplex in the upper hemisphere.</param>
/// <param name="id13_0">The first <c>(1,3)</c> simplex in the lower hemisphere.</param>
/// <param name="id13_1">The second <c>(1,3)</c> simplex in the lower hemisphere.</param>
/// <param name="id13_2">The third <c>(1,3)</c> simplex in the lower hemisphere.</param>
[StructLayout(LayoutKind.Sequential)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct Context(
    VertexId vTarget,
    VertexId u,
    VertexId v,
    VertexId a,
    VertexId b,
    VertexId c,
    SimplexId id31_0,
    SimplexId id31_1,
    SimplexId id31_2,
    SimplexId id13_0,
    SimplexId id13_1,
    SimplexId id13_2)
{
    /// <summary>The central spatial vertex targeted for deletion.</summary>
    internal readonly VertexId VTarget = vTarget;

    /// <summary>The upper pole vertex of the cluster.</summary>
    internal readonly VertexId U = u;

    /// <summary>The lower pole vertex of the cluster.</summary>
    internal readonly VertexId V = v;

    /// <summary>The first equatorial vertex.</summary>
    internal readonly VertexId A = a;

    /// <summary>The second equatorial vertex.</summary>
    internal readonly VertexId B = b;

    /// <summary>The third equatorial vertex.</summary>
    internal readonly VertexId C = c;

    /// <summary>The first <c>(3,1)</c> simplex in the upper hemisphere.</summary>
    internal readonly SimplexId Id31_0 = id31_0;

    /// <summary>The second <c>(3,1)</c> simplex in the upper hemisphere.</summary>
    internal readonly SimplexId Id31_1 = id31_1;

    /// <summary>The third <c>(3,1)</c> simplex in the upper hemisphere.</summary>
    internal readonly SimplexId Id31_2 = id31_2;

    /// <summary>The first <c>(1,3)</c> simplex in the lower hemisphere.</summary>
    internal readonly SimplexId Id13_0 = id13_0;

    /// <summary>The second <c>(1,3)</c> simplex in the lower hemisphere.</summary>
    internal readonly SimplexId Id13_1 = id13_1;

    /// <summary>The third <c>(1,3)</c> simplex in the lower hemisphere.</summary>
    internal readonly SimplexId Id13_2 = id13_2;
}