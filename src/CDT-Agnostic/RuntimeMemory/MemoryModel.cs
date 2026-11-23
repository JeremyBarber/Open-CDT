using CDT_Agnostic.RuntimeMemory.Adjacency;
using CDT_Agnostic.RuntimeMemory.Adjacency.Enumeration;
using CDT_Agnostic.RuntimeMemory.Identifiers;
using CDT_Agnostic.RuntimeMemory.Stores;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_Agnostic.RuntimeMemory;

/// <summary>
/// Dimension-agnostic unmanaged facade over the simplicial graph memory arena.
/// Owns the vertex <see cref="SlotStore"/>, simplex <see cref="SlotStore"/>, and
/// vertex-to-simplex <see cref="AdjacencyMap"/>.
/// </summary>
/// <param name="maxVertices">The maximum number of vertex slots in the arena.</param>
/// <param name="maxSimplices">The maximum number of top-dimensional simplex slots in the arena.</param>
/// <param name="maxAdjacencyBlocks">The total number of 64-byte <see cref="AdjacencyBlock"/> pages in the incidence pool.</param>
[StructLayout(LayoutKind.Sequential)]
public partial struct MemoryModel(uint maxVertices, uint maxSimplices, uint maxAdjacencyBlocks)
{
    private SlotStore _vertices = new(maxVertices);
    private SlotStore _simplices = new(maxSimplices);
    private AdjacencyMap _vertexAdjacency = new(maxAdjacencyBlocks, maxVertices);

    /// <summary>
    /// Releases all unmanaged buffers owned by the vertex store, simplex store, and adjacency map.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Dispose()
    {
        _vertices.Dispose();
        _simplices.Dispose();
        _vertexAdjacency.Dispose();
    }

    // ========================================================================
    // VERTEX SLOTS
    // ========================================================================

    /// <summary>
    /// Gets the number of currently active vertices in the manifold.
    /// </summary>
    public readonly int VertexCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _vertices.Count;
    }

    /// <summary>
    /// Gets the number of unallocated vertex slots remaining on the free-stack.
    /// </summary>
    public readonly int VertexFreeCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _vertices.FreeCount;
    }

    /// <summary>
    /// Allocates a vertex slot in <c>O(1)</c> time and returns its strongly-typed <see cref="VertexId"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VertexId AllocVertex() => new(_vertices.Alloc());

    /// <summary>
    /// Frees the specified <paramref name="id"/> in <c>O(1)</c> time via swap-with-last and returns it to the vertex free-stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FreeVertex(VertexId id) => _vertices.Free(id.Value);

    /// <summary>
    /// Returns the active <see cref="VertexId"/> at dense position <paramref name="pos"/> in <c>[0 .. VertexCount - 1]</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly VertexId GetVertexAt(int pos) => new(_vertices.GetAt(pos));

    // ========================================================================
    // SIMPLEX SLOTS
    // ========================================================================

    /// <summary>
    /// Gets the maximum number of simplices allowed in the current model.
    /// </summary>
    public readonly uint SimplexMaxAllowedCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _simplices.Capacity;
    }

    /// <summary>
    /// Gets the number of currently active top-dimensional simplices in the manifold.
    /// </summary>
    public readonly int SimplexCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _simplices.Count;
    }

    /// <summary>
    /// Gets the number of unallocated simplex slots remaining on the free-stack.
    /// </summary>
    public readonly int SimplexFreeCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _simplices.FreeCount;
    }

    /// <summary>
    /// Allocates a simplex slot in <c>O(1)</c> time and returns its strongly-typed <see cref="SimplexId"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimplexId AllocSimplex() => new(_simplices.Alloc());

    /// <summary>
    /// Frees the specified <paramref name="id"/> in <c>O(1)</c> time via swap-with-last and returns it to the simplex free-stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FreeSimplex(SimplexId id) => _simplices.Free(id.Value);

    /// <summary>
    /// Returns the active <see cref="SimplexId"/> at dense position <paramref name="pos"/> in <c>[0 .. SimplexCount - 1]</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly SimplexId GetSimplexAt(int pos) => new(_simplices.GetAt(pos));

    // ========================================================================
    // ADJACENCY MUTATION & ENUMERATION
    // ========================================================================

    /// <summary>
    /// Registers <paramref name="simplexId"/> in the incident adjacency chain of <paramref name="vertexId"/> in <c>O(1)</c> time.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Link(VertexId vertexId, SimplexId simplexId) =>
        _vertexAdjacency.Add(vertexId.Value, simplexId.Value);

    /// <summary>
    /// Removes <paramref name="simplexId"/> from the incident adjacency chain of <paramref name="vertexId"/> via cross-page swap-and-pop.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unlink(VertexId vertexId, SimplexId simplexId) =>
        _vertexAdjacency.Remove(vertexId.Value, simplexId.Value);

    /// <summary>
    /// Returns a zero-allocation stack enumerator over the 64-byte <see cref="AdjacencyPage"/> blocks incident to <paramref name="vertex"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly AdjacencyPageEnumerator GetIncidentPages(VertexId vertex) =>
        _vertexAdjacency.GetEnumerator(vertex.Value);
}