using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using static CDT_2plus1.Engine.Topology.TopologyConstants;

namespace CDT_2plus1.Engine.Topology;

/// <summary>
/// A single tetrahedron (<c>3</c>-simplex) in the 2+1D triangulation.
/// Memory layout is fixed at 32 bytes (two simplices per 64-byte L1 cache line) so the struct
/// can be packed densely in unmanaged arena memory and queried via 128-bit SIMD instructions.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>Vertices[i]</c> — global vertex ID of local vertex slot <c>i</c> (<c>i = 0..3</c>).</description></item>
/// <item><description><c>NeighborSimplices[i]</c> — global simplex ID of the neighbor across the facet opposite local vertex <c>i</c>.</description></item>
/// </list>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Tetrahedron
{
    /// <summary>
    /// The global IDs of the 4 vertices forming the tetrahedron.
    /// </summary>
    public fixed int Vertices[VerticesPerSimplex];

    /// <summary>
    /// The global IDs of the 4 adjacent tetrahedra.
    /// An ID of <c>-1</c> (<c>InvalidData</c>) indicates an unlinked or boundary face.
    /// </summary>
    public fixed int NeighborSimplices[FacetsPerSimplex];

    /// <summary>
    /// Uses 128-bit SIMD intrinsics to find the local slot index <c>[0..3]</c> of a given global <see cref="VertexId"/>.
    /// </summary>
    /// <param name="vertexId">The global <see cref="VertexId"/> to search for.</param>
    /// <returns>
    /// The local slot index <c>[0..3]</c>, or <c>32</c> if not found.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly LocalVertexSlot FindVertexSlot(VertexId vertexId) =>
        new(FindSlotIndex(in Vertices[0], vertexId.Value));

    /// <summary>
    /// Uses 128-bit SIMD intrinsics to find the local boundary facet <c>[0..3]</c> that points to a specific neighbor.
    /// </summary>
    /// <param name="simplexId">The global ID of the neighboring simplex.</param>
    /// <returns>
    /// The local facet index <c>[0..3]</c>, or <c>32</c> if not found.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly LocalFacetSlot GetFacetFacingSimplexIndex(SimplexId simplexId) =>
        new((byte)FindSlotIndex(in NeighborSimplices[0], simplexId.Value));

    /// <summary>
    /// Identifies the two vertices in the tetrahedron that do NOT belong to the specified edge.
    /// Evaluated strictly using branchless bitwise masking to avoid loop overhead.
    /// </summary>
    /// <param name="vertexIdA">The first vertex of the excluded edge.</param>
    /// <param name="vertexIdB">The second vertex of the excluded edge.</param>
    /// <returns>A tuple containing the global IDs of the two non-edge vertices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly (VertexId X, VertexId Y) GetNonEdgeVertices(VertexId vertexIdA, VertexId vertexIdB)
    {
        // Find the local slots (0..3) for the two edge vertices
        var slotA = FindSlotIndex(in Vertices[0], vertexIdA.Value);
        var slotB = FindSlotIndex(in Vertices[0], vertexIdB.Value);

        // The mask is a 4-bit number where the edge vertices are 1s and the non-edge vertices are 0s.
        var mask = (1 << slotA) | (1 << slotB);

        // Invert the bottom 4 bits to highlight the non-edge vertices.
        var invertedMask = (~mask) & 0b1111;

        // Extract the index of the first 1-bit in the inverted mask
        var slotX = BitOperations.TrailingZeroCount(invertedMask);

        // Clear the lowest set bit to find the second vertex
        var clearedMask = invertedMask & (invertedMask - 1);
        var slotY = BitOperations.TrailingZeroCount(clearedMask);

        return (new VertexId(Vertices[slotX]), new VertexId(Vertices[slotY]));
    }

    /// <summary>
    /// Maps a logical 1D edge index <c>[0..5]</c> to its two constituent local vertex slots.
    /// </summary>
    /// <param name="localEdgeIndex">The local edge identifier <c>[0..5]</c>.</param>
    /// <returns>A tuple containing the local vertex slots <c>[0..3]</c> that define the edge.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (LocalVertexSlot SlotA, LocalVertexSlot SlotB) GetEdgeVertexSlots(LocalEdgeSlot localEdgeIndex)
    {
        // The JIT compiler places these directly in the static PE data segment with zero heap allocation.
        ReadOnlySpan<byte> edgeV1 = [0, 0, 0, 1, 1, 2];
        ReadOnlySpan<byte> edgeV2 = [1, 2, 3, 2, 3, 3];

        return (new LocalVertexSlot(edgeV1[localEdgeIndex.Value]), new LocalVertexSlot(edgeV2[localEdgeIndex.Value]));
    }

    /// <summary>
    /// Searches a 128-bit (<c>4 × 32</c>-bit) memory block for a target integer in a single SIMD comparison.
    /// </summary>
    /// <param name="firstElement">A read-only reference to the first element of the 4-integer sequence.</param>
    /// <param name="targetValue">The 32-bit integer to search for.</param>
    /// <returns>The index <c>[0..3]</c> of the match, or <c>32</c> if no match is found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindSlotIndex(ref readonly int firstElement, int targetValue)
    {
        // 1. Read 16 bytes (4 x 32-bit integers) directly into a 128-bit SIMD register.
        var block = Unsafe.ReadUnaligned<Vector128<int>>(
            ref Unsafe.As<int, byte>(ref Unsafe.AsRef(in firstElement)));

        // 2. Broadcast the target value to all 4 lanes.
        var target = Vector128.Create(targetValue);

        // 3. Compare all 4 lanes simultaneously. Matches become 0xFFFFFFFF, mismatches become 0.
        var match = Vector128.Equals(block, target);

        // 4. Extract the most significant bit of each 32-bit lane to create a 4-bit mask.
        var mask = match.ExtractMostSignificantBits();

        // 5. Use hardware bit-scanning to find the index:
        // Slot 0 match -> mask is 0b0001 -> 0 trailing zeros
        // Slot 1 match -> mask is 0b0010 -> 1 trailing zero
        // Slot 2 match -> mask is 0b0100 -> 2 trailing zeros
        // Slot 3 match -> mask is 0b1000 -> 3 trailing zeros
        // No match     -> mask is 0b0000 -> 32 trailing zeros
        return BitOperations.TrailingZeroCount(mask);
    }
}