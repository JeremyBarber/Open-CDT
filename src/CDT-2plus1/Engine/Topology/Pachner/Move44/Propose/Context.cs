using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_2plus1.Engine.Topology.Pachner.Move44.Propose;

/// <summary>
/// The isolated geometric state required to safely execute a <c>(4,4)</c> Pachner move.
/// In 2+1D CDT, this move performs a volume-preserving spatial edge flip, replacing
/// the shared spacelike diagonal <c>(VA, VB)</c> with the alternate spacelike diagonal <c>(C, D)</c>
/// across a 4-simplex bipyramid (two <c>(3,1)</c> and two <c>(1,3)</c> simplices).
/// </summary>
/// <param name="vA">The first vertex of the existing central spacelike edge being flipped.</param>
/// <param name="vB">The second vertex of the existing central spacelike edge being flipped.</param>
/// <param name="c">The first opposite equatorial vertex of the new spacelike diagonal.</param>
/// <param name="d">The second opposite equatorial vertex of the new spacelike diagonal.</param>
/// <param name="id0">The first simplex in the 4-simplex cluster.</param>
/// <param name="id1">The second simplex in the 4-simplex cluster.</param>
/// <param name="id2">The third simplex in the 4-simplex cluster.</param>
/// <param name="id3">The fourth simplex in the 4-simplex cluster.</param>
[StructLayout(LayoutKind.Sequential)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct Context(
    VertexId vA,
    VertexId vB,
    VertexId c,
    VertexId d,
    SimplexId id0,
    SimplexId id1,
    SimplexId id2,
    SimplexId id3)
{
    /// <summary>The first vertex of the current central spacelike edge.</summary>
    internal readonly VertexId VA = vA;

    /// <summary>The second vertex of the current central spacelike edge.</summary>
    internal readonly VertexId VB = vB;

    /// <summary>The first opposite equatorial vertex of the alternate diagonal.</summary>
    internal readonly VertexId C = c;

    /// <summary>The second opposite equatorial vertex of the alternate diagonal.</summary>
    internal readonly VertexId D = d;

    /// <summary>The first simplex in the 4-simplex cluster.</summary>
    internal readonly SimplexId Id0 = id0;

    /// <summary>The second simplex in the 4-simplex cluster.</summary>
    internal readonly SimplexId Id1 = id1;

    /// <summary>The third simplex in the 4-simplex cluster.</summary>
    internal readonly SimplexId Id2 = id2;

    /// <summary>The fourth simplex in the 4-simplex cluster.</summary>
    internal readonly SimplexId Id3 = id3;
}