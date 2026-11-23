using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using static CDT_Agnostic.RuntimeMemory.Native.Allocation;

namespace CDT_Agnostic.RuntimeMemory.Stores;

/// <summary>
/// Unmanaged <c>O(1)</c> slot allocator and dense active-set tracker based on a Briggs-Torczon sparse-set
/// paired with a LIFO recycling free-stack.
/// <para>
/// Owns three unmanaged integer buffers of length <c>maxSlots</c>:
/// <list type="bullet">
/// <item><description><c>_active</c>: Densely packed array of currently live slot IDs in <c>[0 .. Count - 1]</c>, enabling <c>O(1)</c> uniform random sampling.</description></item>
/// <item><description><c>_activeIndex</c>: Inverse lookup table mapping a live slot <c>id</c> to its index in <c>_active</c> (<c>_active[_activeIndex[id]] == id</c>).</description></item>
/// <item><description><c>_freeStack</c>: LIFO stack of currently unallocated slot IDs available for immediate <c>O(1)</c> reuse.</description></item>
/// </list>
/// </para>
/// </summary>
internal unsafe partial struct SlotStore
{
    private readonly int* _active;
    private readonly int* _activeIndex;
    private readonly int* _freeStack;

    private int _activeCount;
    private int _freeHead;

    /// <summary>
    /// Gets the number of currently live slots in the dense <c>_active</c> array.
    /// </summary>
    internal readonly int Count => _activeCount;

    /// <summary>
    /// Gets the number of unallocated slots remaining on the free-stack.
    /// </summary>
    internal readonly int FreeCount => _freeHead;

    /// <summary>
    /// Allocates all three unmanaged buffers and seeds the free-stack with every slot index in <c>[0 .. maxSlots - 1]</c>.
    /// </summary>
    /// <param name="maxSlots">The hard upper capacity of slots managed by this store.</param>
    internal SlotStore(uint maxSlots)
    {
        _active = Allocate<int>(maxSlots);
        _activeIndex = Allocate<int>(maxSlots);
        _freeStack = Allocate<int>(maxSlots);

        _activeCount = 0;
        _freeHead = (int)maxSlots;

        // Seed in reverse order so initial Alloc() calls hand out IDs 0, 1, 2, ... sequentially.
        // Subsequent Free(id) / Alloc() cycles operate in standard LIFO stack order for L1 cache warmth.
        for (var i = 0; i < maxSlots; i++)
            _freeStack[i] = (int)maxSlots - 1 - i;
    }

    /// <summary>
    /// Releases all unmanaged buffers owned by this instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly void Dispose()
    {
        SafeFree(_active);
        SafeFree(_activeIndex);
        SafeFree(_freeStack);
    }

    /// <summary>
    /// Returns the live slot ID stored at dense index <paramref name="pos"/> in <c>[0 .. Count - 1]</c>.
    /// </summary>
    /// <param name="pos">The dense index within the packed active list.</param>
    /// <returns>The live slot ID at <paramref name="pos"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly int GetAt(int pos) => _active[pos];

    /// <summary>
    /// Pops a free slot ID from the LIFO stack, appends it to the dense active list, and records its inverse position in <c>O(1)</c> time.
    /// </summary>
    /// <returns>The newly allocated slot ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when all <c>maxSlots</c> are already allocated.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Alloc()
    {
        if (_freeHead == 0)
            ThrowExhausted();

        var id = _freeStack[--_freeHead];
        var pos = _activeCount++;

        _active[pos] = id;
        _activeIndex[id] = pos;

        return id;
    }

    /// <summary>
    /// Removes <paramref name="id"/> from the dense active list via <c>O(1)</c> swap-with-last
    /// (updating the displaced slot's inverse index) and pushes <paramref name="id"/> onto the LIFO free-stack.
    /// </summary>
    /// <remarks>
    /// Caller contract: <paramref name="id"/> must be currently live. Freeing an unallocated or already-freed ID
    /// is unchecked in release builds to preserve hot-loop performance.
    /// </remarks>
    /// <param name="id">The live slot ID to free.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Free(int id)
    {
        var pos = _activeIndex[id];
        var displaced = _active[--_activeCount];

        _active[pos] = displaced;
        _activeIndex[displaced] = pos;
        _freeStack[_freeHead++] = id;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowExhausted() =>
        throw new InvalidOperationException("Slot Store exhausted");
}