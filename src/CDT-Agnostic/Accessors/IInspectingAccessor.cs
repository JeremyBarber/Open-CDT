using CDT_Agnostic.RuntimeMemory.Identifiers;

namespace CDT_Agnostic.Accessors;

/// <summary>
/// A zero-allocation, read-only lens into the unmanaged CDT Manifold.
/// Provides strictly O(1) pointer-math lookups for simulation read operations.
/// </summary>
/// <remarks>
/// Bounds checking is intentionally omitted on all read operations to preserve L1 cache performance.
/// </remarks>
public interface IInspectingAccessor
{
    /// <summary>
    /// The number of facets (sub-simplices of dimension D-1) bounding a single simplex.
    /// </summary>
    static abstract int FacetsPerSimplex { get; }

    /// <summary>
    /// The number of vertices (nodes) that make up a single simplex.
    /// </summary>
    static abstract int VerticesPerSimplex { get; }

    /// <summary>
    /// Reads the global ID of the vertex at the specified local slot of a simplex.
    /// </summary>
    /// <param name="simplexId">The unique identifier of the target simplex.</param>
    /// <param name="vertexIndex">The local index of the vertex.</param>
    /// <returns>The global VertexId.</returns>
    VertexId GetVertex(SimplexId simplexId, int vertexIndex);

    /// <summary>
    /// Reads the integer timeslice foliation for a specific vertex.
    /// </summary>
    /// <param name="vertexId">The global identifier of the vertex.</param>
    /// <returns>The discrete time coordinate of the vertex.</returns>
    TimeSlice GetVertexTime(VertexId vertexId);

    /// <summary>
    /// Reads the ID of the simplex sharing the specified face index.
    /// </summary>
    /// <param name="simplexId">The source simplex ID.</param>
    /// <param name="faceIndex">The local index of the face.</param>
    /// <returns>The ID of the neighboring simplex.</returns>
    SimplexId GetNeighbor(SimplexId simplexId, int faceIndex);
}

/// <summary>
/// A zero-allocation, read-only lens into the unmanaged CDT manifold,
/// exposing an integrated dimension-agnostic vertex-simplex adjacency lens.
/// </summary>
/// <typeparam name="TAdjacency">The concrete unmanaged <see cref="IAdjacencyAccessor"/> implementation.</typeparam>
public interface IInspectingAccessor<out TAdjacency> : IInspectingAccessor
    where TAdjacency : unmanaged, IAdjacencyAccessor
{
    /// <summary>
    /// Gets a zero-allocation, read-only lens into the underlying vertex-simplex adjacency graph.
    /// </summary>
    TAdjacency Adjacency { get; }
}