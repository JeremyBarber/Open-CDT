using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.RuntimeMemory;
using CDT_Agnostic.RuntimeMemory.Stores;

using System.Runtime.InteropServices;

using static CDT_Agnostic.RuntimeMemory.Native.Allocation;

namespace CDT_2plus1.Engine.State.Mesh;

/// <summary>
/// Owns all unmanaged memory buffers for the 2+1D Causal Dynamical Triangulations (CDT) spacetime manifold.
/// Lives entirely on the native OS heap with zero Garbage Collector tracking.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe partial struct Manifold
{
    /// <summary>
    /// 64-byte cache-line-aligned slab array of 32-byte <see cref="Tetrahedron"/> structs, indexed by <c>SimplexId</c>.
    /// Guarantees two simplices per CPU L1 cache line with zero cache-line splits.
    /// </summary>
    private Tetrahedron* SimplexPool;

    /// <summary>Dense byte array caching the <see cref="SimplexType"/> of each simplex slot, indexed by <c>SimplexId</c>.</summary>
    private byte* SimplexTypes;

    /// <summary>
    /// Discrete proper-time foliation coordinate (<c>t</c>) for each vertex, indexed by <c>VertexId</c>.
    /// Initialized to <c>-1</c> (<c>0xFFFFFFFF</c>) for unallocated vertex slots.
    /// </summary>
    private int* VertexTimes;

    /// <summary>Dimension-agnostic vertex/simplex slot stores and per-vertex incidence chains.</summary>
    private MemoryModel Memory;

    /// <summary>Active-set tracker for <c>O(1)</c> random selection of <c>(3,1)</c> simplices.</summary>
    private TypeStore TypeStore31;

    /// <summary>Active-set tracker for <c>O(1)</c> random selection of <c>(2,2)</c> simplices.</summary>
    private TypeStore TypeStore22;

    /// <summary>Active-set tracker for <c>O(1)</c> random selection of <c>(1,3)</c> simplices.</summary>
    private TypeStore TypeStore13;

    /// <summary>
    /// Gets the maximum number of simplices allowed in the current model.
    /// </summary>
    internal readonly uint MaxSimplices => Memory.SimplexMaxAllowedCount;

    /// <summary>
    /// Allocates a new <see cref="Manifold"/> and all of its backing native memory arenas on the unmanaged heap.
    /// </summary>
    /// <param name="maxVertices">The maximum number of concurrent vertices supported by the arena (must be &gt; 0).</param>
    /// <param name="maxSimplices">The maximum number of concurrent simplices supported by the arena (must be &gt; 0).</param>
    /// <param name="maxAdjacencyBlocks">The maximum number of 64-byte adjacency pages in the incidence pool (must be &gt; 0).</param>
    /// <returns>An unmanaged pointer to the initialized <see cref="Manifold"/>.</returns>
    internal static Manifold* Create(uint maxVertices, uint maxSimplices, uint maxAdjacencyBlocks)
    {
        ArgumentOutOfRangeException.ThrowIfZero(maxVertices);
        ArgumentOutOfRangeException.ThrowIfZero(maxSimplices);
        ArgumentOutOfRangeException.ThrowIfZero(maxAdjacencyBlocks);

        var state = Allocate<Manifold>(1);

        // Allocate SimplexPool on a 64-byte L1 cache-line boundary so every 32-byte Tetrahedron
        // sits completely inside a single cache line.
        state->SimplexPool = AllocateAligned<Tetrahedron>(maxSimplices);
        state->SimplexTypes = Allocate<byte>(maxSimplices);
        state->VertexTimes = Allocate<int>(maxVertices);

        // Initialize all simplex vertices/neighbors, type markers, and vertex timeslices to Invalid (-1 / 0xFF).
        Fill(state->SimplexPool, maxSimplices);
        Fill(state->SimplexTypes, maxSimplices);
        Fill(state->VertexTimes, maxVertices);

        state->Memory = new MemoryModel(maxVertices, maxSimplices, maxAdjacencyBlocks);

        state->TypeStore31 = new TypeStore(maxSimplices);
        state->TypeStore22 = new TypeStore(maxSimplices);
        state->TypeStore13 = new TypeStore(maxSimplices);

        return state;
    }

    /// <summary>
    /// Disposes all internal unmanaged buffers, frees the <see cref="Manifold"/> struct itself,
    /// and nulls the caller's pointer reference to prevent use-after-free faults.
    /// </summary>
    /// <param name="state">Reference to the <see cref="Manifold"/> pointer to destroy (safe to pass <see langword="null"/>).</param>
    internal static void Destroy(ref Manifold* state)
    {
        if (state == null)
            return;

        state->Dispose();
        NativeMemory.Free(state);

        state = null;
    }

    private void Dispose()
    {
        Memory.Dispose();
        Memory = default;

        TypeStore13.Dispose();
        TypeStore13 = default;

        TypeStore22.Dispose();
        TypeStore22 = default;

        TypeStore31.Dispose();
        TypeStore31 = default;

        SafeFreeAligned(SimplexPool);
        SimplexPool = null;

        SafeFree(SimplexTypes);
        SimplexTypes = null;

        SafeFree(VertexTimes);
        VertexTimes = null;
    }
}