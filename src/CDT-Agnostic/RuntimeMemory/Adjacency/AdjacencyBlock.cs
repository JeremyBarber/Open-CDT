using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Adjacency;

/// <summary>
/// A single fixed-size page in an entity's unrolled linked adjacency chain.
/// Explicitly sized to 64 bytes—matching a single x86-64 / ARM64 CPU L1 cache line—to guarantee
/// zero cache-line splits during SIMD adjacency scans.
/// <para>
/// Memory layout (64 bytes total):
/// <list type="bullet">
/// <item><description><c>Ids[14]</c>: 14 integer IDs × 4 bytes = 56 bytes (offsets 0..55)</description></item>
/// <item><description><c>Next</c>: Next block index in pool = 4 bytes (offsets 56..59)</description></item>
/// <item><description><c>Count</c>: Active entry count in page = 4 bytes (offsets 60..63)</description></item>
/// </list>
/// </para>
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = CacheLineBytes)]
public unsafe struct AdjacencyBlock
{
    /// <summary>
    /// The maximum number of integer IDs stored within a single 64-byte cache-line block.
    /// </summary>
    public const int Capacity = 14;

    [FieldOffset(0)] internal fixed int Ids[Capacity];
    [FieldOffset(56)] internal int Next;
    [FieldOffset(60)] internal int Count;

    /// <summary>
    /// Computes a 14-bit bitmask indicating which active lanes in <c>[0 .. count - 1]</c> match <paramref name="targetId"/>.
    /// Evaluates the entire 64-byte cache line branchlessly using hardware SIMD instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint GetMatchMask(int* ids, int count, int targetId)
    {
        var validMask = (1u << count) - 1u;

        var vTarget = Vector256.Create(targetId);
        var m0 = Vector256.Equals(Vector256.Load(ids), vTarget).ExtractMostSignificantBits();
        var m1 = Vector256.Equals(Vector256.Load(ids + 8), vTarget).ExtractMostSignificantBits();

        return (m0 | (m1 << 8)) & validMask;
    }
}