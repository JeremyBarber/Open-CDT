using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_2plus1.Engine.Topology.Pachner.Move26.Propose;

/// <summary>
/// The isolated geometric state required to safely execute a <c>(2,6)</c> Pachner move.
/// In strictly foliated 2+1D CDT, this move inserts a vertex into the center of a
/// spacelike triangle shared by a <c>(1,3)</c> and a <c>(3,1)</c> simplex, driving spatial expansion.
/// </summary>
/// <param name="id13">The global ID of the downward-facing <c>(1,3)</c> simplex.</param>
/// <param name="id31">The global ID of the upward-facing <c>(3,1)</c> simplex.</param>
/// <param name="slotDown">The local vertex slot of the downward pole in the <c>(1,3)</c> simplex.</param>
/// <param name="timeEquator">The temporal slice index where the shared spatial face resides.</param>
[StructLayout(LayoutKind.Sequential)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct Context(
    SimplexId id13,
    SimplexId id31,
    LocalVertexSlot slotDown,
    TimeSlice timeEquator)
{
    /// <summary>The global ID of the downward-facing <c>(1,3)</c> simplex.</summary>
    internal readonly SimplexId Id13 = id13;

    /// <summary>The global ID of the upward-facing <c>(3,1)</c> simplex.</summary>
    internal readonly SimplexId Id31 = id31;

    /// <summary>The temporal slice index of the shared spatial face.</summary>
    internal readonly TimeSlice TimeEquator = timeEquator;

    /// <summary>The local vertex slot pointing to the downward pole in <see cref="Id13"/>.</summary>
    internal readonly LocalVertexSlot SlotDown = slotDown;
}