using CDT_Agnostic.RuntimeMemory.Adjacency.Enumeration;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using static CDT_Agnostic.RuntimeMemory.Native.Allocation;
using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Adjacency;

/// <summary>
/// Unmanaged, cache-line-aligned unrolled linked list mapping each entity (e.g., a vertex)
/// to its dynamically changing set of incident integer IDs (e.g., simplices).
/// <para>
/// Each entity owns a chain of 64-byte <see cref="AdjacencyBlock"/> pages. Entries across the chain
/// are kept densely packed with zero gaps: insertions append to the tail page in <c>O(1)</c> time,
/// and removals overwrite the deleted slot using the last element of the tail page (cross-page swap-and-pop).
/// </para>
/// <para>
/// Owns four unmanaged buffers:
/// <list type="bullet">
/// <item><description><c>_pool</c>: 64-byte aligned slab array of all <see cref="AdjacencyBlock"/> pages.</description></item>
/// <item><description><c>_entityHeads</c>: Index of the first block in each entity's chain (<c>-1</c> if empty).</description></item>
/// <item><description><c>_entityTails</c>: Index of the last block in each entity's chain for <c>O(1)</c> appends and swap-and-pop removals.</description></item>
/// <item><description><c>_freeStack</c>: LIFO stack of currently unused block indices in <c>_pool</c>.</description></item>
/// </list>
/// </para>
/// </summary>
internal unsafe partial struct AdjacencyMap
{
    private readonly AdjacencyBlock* _pool;
    private readonly int* _entityHeads;
    private readonly int* _entityTails;
    private readonly int* _freeStack;

    private int _freeHead;

    /// <summary>
    /// Allocates all four unmanaged buffers and initializes the LIFO block free-stack.
    /// </summary>
    /// <param name="maxBlocks">Total capacity of the <see cref="AdjacencyBlock"/> pool.</param>
    /// <param name="maxEntities">Maximum number of distinct entities (length of <c>_entityHeads</c> and <c>_entityTails</c>).</param>
    internal AdjacencyMap(uint maxBlocks, uint maxEntities)
    {
        _pool = AllocateAligned<AdjacencyBlock>(maxBlocks);
        _entityHeads = Allocate<int>(maxEntities);
        _entityTails = Allocate<int>(maxEntities);
        _freeStack = Allocate<int>(maxBlocks);

        _freeHead = (int)maxBlocks;

        // 0xFF repeated across every byte produces 0xFFFFFFFF per 32-bit int (-1 in two's complement),
        // initializing every Next, Count, head, and tail slot to InvalidData (-1) in a single memset.
        Fill(_pool, maxBlocks);
        Fill(_entityHeads, maxEntities);
        Fill(_entityTails, maxEntities);

        for (var i = 0; i < maxBlocks; i++)
            _freeStack[i] = i;
    }

    /// <summary>
    /// Releases all unmanaged memory buffers owned by this instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly void Dispose()
    {
        SafeFreeAligned(_pool);
        SafeFree(_entityHeads);
        SafeFree(_entityTails);
        SafeFree(_freeStack);
    }

    /// <summary>
    /// Returns a zero-allocation stack enumerator over all <see cref="AdjacencyPage"/> blocks for <paramref name="entityId"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly AdjacencyPageEnumerator GetEnumerator(int entityId) =>
        new(_entityHeads[entityId], _pool);

    /// <summary>
    /// Pops an unused <see cref="AdjacencyBlock"/> from the LIFO free-stack and initializes it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RentBlock()
    {
        if (_freeHead == 0)
            ThrowExhausted();

        var idx = _freeStack[--_freeHead];
        var b = &_pool[idx];
        b->Count = 0;
        b->Next = InvalidData;
        return idx;
    }

    /// <summary>
    /// Pushes an emptied <see cref="AdjacencyBlock"/> index back onto the LIFO free-stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnBlock(int idx) => _freeStack[_freeHead++] = idx;

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowExhausted() =>
        throw new InvalidOperationException("Adjacency Map block pool exhausted");
}