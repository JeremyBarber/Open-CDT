using System.Runtime.CompilerServices;

namespace CDT_Agnostic.RuntimeMemory.Adjacency.Enumeration;

/// <summary>
/// A lightweight, stack-only read-only view over the active integer IDs stored within a single <see cref="AdjacencyBlock"/>.
/// </summary>
public readonly unsafe ref struct AdjacencyPage
{
    /// <summary>
    /// Pointer to the first element of the page's contiguous integer ID buffer.
    /// </summary>
    public readonly int* Data;

    /// <summary>
    /// The number of valid, densely packed IDs currently stored in this page (<c>0 .. AdjacencyBlock.Capacity</c>).
    /// </summary>
    public readonly int Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal AdjacencyPage(int* data, int count)
    {
        Data = data;
        Count = count;
    }
}