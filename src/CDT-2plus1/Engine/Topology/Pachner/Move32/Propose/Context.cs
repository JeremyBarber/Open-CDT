using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_2plus1.Engine.Topology.Pachner.Move32.Propose;

/// <summary>
/// The isolated geometric state required to safely execute a <c>(3,2)</c> Pachner move.
/// In 2+1D CDT, this move collapses a timelike edge shared by three simplices
/// (two <c>(2,2)</c> simplices and one asymmetric <c>(3,1)</c>/<c>(1,3)</c> simplex) down to a single timelike face.
/// </summary>
/// <param name="id0">The global ID of the first simplex sharing the timelike edge.</param>
/// <param name="id1">The global ID of the second simplex sharing the timelike edge.</param>
/// <param name="id2">The global ID of the third simplex sharing the timelike edge.</param>
/// <param name="idAsym">The specific ID of the asymmetric <c>(3,1)</c> or <c>(1,3)</c> simplex in the cluster.</param>
/// <param name="vA">The first vertex (pole) of the shared timelike edge.</param>
/// <param name="vB">The second vertex (pole) of the shared timelike edge.</param>
/// <param name="p">The first bounding triangle vertex surrounding the edge.</param>
/// <param name="q">The second bounding triangle vertex surrounding the edge.</param>
/// <param name="r">The third bounding triangle vertex surrounding the edge.</param>
[StructLayout(LayoutKind.Sequential)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct Context(
    SimplexId id0,
    SimplexId id1,
    SimplexId id2,
    SimplexId idAsym,
    VertexId vA,
    VertexId vB,
    VertexId p,
    VertexId q,
    VertexId r)
{
    /// <summary>The first simplex sharing the central timelike edge.</summary>
    internal readonly SimplexId Id0 = id0;

    /// <summary>The second simplex sharing the central timelike edge.</summary>
    internal readonly SimplexId Id1 = id1;

    /// <summary>The third simplex sharing the central timelike edge.</summary>
    internal readonly SimplexId Id2 = id2;

    /// <summary>The specific asymmetric <c>(3,1)</c> or <c>(1,3)</c> simplex in the cluster.</summary>
    internal readonly SimplexId IdAsym = idAsym;

    /// <summary>The first pole of the central timelike edge.</summary>
    internal readonly VertexId VA = vA;

    /// <summary>The second pole of the central timelike edge.</summary>
    internal readonly VertexId VB = vB;

    /// <summary>The first bounding vertex surrounding the central edge.</summary>
    internal readonly VertexId P = p;

    /// <summary>The second bounding vertex surrounding the central edge.</summary>
    internal readonly VertexId Q = q;

    /// <summary>The third bounding vertex surrounding the central edge.</summary>
    internal readonly VertexId R = r;
}