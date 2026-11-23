using CDT_Agnostic.RuntimeMemory.Identifiers;

namespace CDT_Agnostic.Accessors;

/// <summary>
/// A zero-allocation, read-only lens into the dimension-agnostic vertex-to-simplex incidence graph.
/// </summary>
public interface IAdjacencyAccessor
{
    /// <summary>
    /// Gets the total number of currently active simplices in the simulated universe.
    /// </summary>
    int TotalSimplices { get; }

    /// <summary>
    /// Gets the total number of currently active vertices in the simulated universe.
    /// </summary>
    int TotalVertices { get; }

    /// <summary>
    /// Resolves the Nth active simplex from the underlying dense active-set pool.
    /// </summary>
    /// <param name="index">The dense array index in <c>[0 .. TotalSimplices - 1]</c>.</param>
    /// <returns>The active <see cref="SimplexId"/> at that dense index.</returns>
    SimplexId GetNthSimplexId(int index);

    /// <summary>
    /// Determines whether a topological 1-simplex (edge) exists between two vertices.
    /// </summary>
    /// <param name="vertexA">The first vertex of the queried edge.</param>
    /// <param name="vertexB">The second vertex of the queried edge.</param>
    /// <returns><see langword="true"/> if the vertices share at least one active simplex; otherwise, <see langword="false"/>.</returns>
    bool EdgeExists(VertexId vertexA, VertexId vertexB);

    /// <summary>
    /// Determines whether a topological 2-simplex (triangle) exists between three vertices.
    /// </summary>
    /// <param name="vertexA">The first vertex of the queried triangle.</param>
    /// <param name="vertexB">The second vertex of the queried triangle.</param>
    /// <param name="vertexC">The third vertex of the queried triangle.</param>
    /// <returns><see langword="true"/> if all three vertices share at least one active simplex; otherwise, <see langword="false"/>.</returns>
    bool TriangleExists(VertexId vertexA, VertexId vertexB, VertexId vertexC);

    /// <summary>
    /// Extracts the exact sequence of simplices sharing a specific edge.
    /// Fails fast without overflowing <paramref name="buffer"/> if the shared count does not equal <paramref name="expected"/>.
    /// </summary>
    /// <param name="vertexA">The first vertex of the edge.</param>
    /// <param name="vertexB">The second vertex of the edge.</param>
    /// <param name="buffer">Caller-allocated unmanaged buffer of length at least <paramref name="expected"/>.</param>
    /// <param name="expected">The exact number of shared simplices required.</param>
    /// <returns><see langword="true"/> if the exact count was found and written to <paramref name="buffer"/>; otherwise, <see langword="false"/>.</returns>
    unsafe bool TryGetExactlyNSharedSimplices(VertexId vertexA, VertexId vertexB, SimplexId* buffer, int expected);

    /// <summary>
    /// Extracts the exact sequence of simplices incident to a specific vertex.
    /// Fails fast without overflowing <paramref name="buffer"/> if the incident count does not equal <paramref name="expected"/>.
    /// </summary>
    /// <param name="vertexId">The target central vertex.</param>
    /// <param name="buffer">Caller-allocated unmanaged buffer of length at least <paramref name="expected"/>.</param>
    /// <param name="expected">The exact number of incident simplices required.</param>
    /// <returns><see langword="true"/> if the exact count was found and written to <paramref name="buffer"/>; otherwise, <see langword="false"/>.</returns>
    unsafe bool GetExactlyNSimplices(VertexId vertexId, SimplexId* buffer, int expected);
}