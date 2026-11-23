using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_2plus1.Engine.Topology.Pachner.Move23.Propose;

/// <summary>
/// The isolated local geometric state required to execute a <c>(2,3)</c> Pachner move.
/// Holds the pre-validated topology of a timelike triangle shared between a <c>(2,2)</c> simplex (<see cref="Id0"/>)
/// and an adjacent <c>(3,1)</c> or <c>(1,3)</c> simplex (<see cref="Id1"/>).
/// </summary>
/// <param name="id0">The arena ID of the <c>(2,2)</c> simplex.</param>
/// <param name="id1">The arena ID of the adjacent <c>(3,1)</c> or <c>(1,3)</c> simplex.</param>
/// <param name="faceSlot0">The local facet slot <c>[0..3]</c> on <paramref name="id0"/> glued to <paramref name="id1"/>.</param>
/// <param name="faceSlot1">The local facet slot <c>[0..3]</c> on <paramref name="id1"/> glued to <paramref name="id0"/>.</param>
/// <param name="vTarget">The apex vertex in <paramref name="id0"/> opposite the shared timelike face.</param>
/// <param name="vNeighbour">The apex vertex in <paramref name="id1"/> opposite the shared timelike face.</param>
[StructLayout(LayoutKind.Sequential)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct Context(
    SimplexId id0,
    SimplexId id1,
    LocalFacetSlot faceSlot0,
    LocalFacetSlot faceSlot1,
    VertexId vTarget,
    VertexId vNeighbour)
{
    /// <summary>The arena ID of the initial <c>(2,2)</c> simplex.</summary>
    internal readonly SimplexId Id0 = id0;

    /// <summary>The arena ID of the adjacent <c>(3,1)</c> or <c>(1,3)</c> simplex.</summary>
    internal readonly SimplexId Id1 = id1;

    /// <summary>The local boundary facet on <see cref="Id0"/> facing <see cref="Id1"/>.</summary>
    internal readonly LocalFacetSlot FaceSlot0 = faceSlot0;

    /// <summary>The local boundary facet on <see cref="Id1"/> facing <see cref="Id0"/>.</summary>
    internal readonly LocalFacetSlot FaceSlot1 = faceSlot1;

    /// <summary>The unshared pole vertex belonging to <see cref="Id0"/>.</summary>
    internal readonly VertexId VTarget = vTarget;

    /// <summary>The unshared pole vertex belonging to <see cref="Id1"/>.</summary>
    internal readonly VertexId VNeighbour = vNeighbour;
}