using System.Runtime.CompilerServices;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Adjacency.Enumeration;

/// <summary>
/// A zero-allocation, stack-only <c>ref struct</c> enumerator that walks the linked chain
/// of <see cref="AdjacencyBlock"/> pages belonging to a single entity.
/// </summary>
public unsafe ref struct AdjacencyPageEnumerator
{
    private int _nextCursor;
    private int _currentCursor;
    private readonly AdjacencyBlock* _pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal AdjacencyPageEnumerator(int headCursor, AdjacencyBlock* pool)
    {
        _nextCursor = headCursor;
        _currentCursor = InvalidData;
        _pool = pool;
    }

    /// <summary>
    /// Gets the current <see cref="AdjacencyPage"/> slice pointed to by the enumerator.
    /// </summary>
    public readonly AdjacencyPage Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var page = &_pool[_currentCursor];
            return new AdjacencyPage(page->Ids, page->Count);
        }
    }

    /// <summary>
    /// Advances the enumerator to the next <see cref="AdjacencyBlock"/> page in the entity's chain.
    /// </summary>
    /// <returns><see langword="true"/> if the enumerator advanced to a valid page; <see langword="false"/> if the end of the chain was reached.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_nextCursor == InvalidData)
            return false;

        _currentCursor = _nextCursor;
        _nextCursor = _pool[_currentCursor].Next;
        return true;
    }

    /// <summary>
    /// Returns this instance to support C# <c>foreach</c> duck-typing without boxing or allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly AdjacencyPageEnumerator GetEnumerator() => this;
}