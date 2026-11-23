namespace CDT_Agnostic.RuntimeMemory;

public partial struct MemoryModel
{
    /// <summary>
    /// Recovers the arena's <c>(MaxVertices, MaxSimplices, MaxAdjacencyBlocks)</c> capacities directly
    /// from the conserved active + free slot invariants of its underlying stores.
    /// </summary>
    /// <returns>A tuple containing the total vertex capacity, simplex capacity, and adjacency block capacity.</returns>
    public readonly (uint MaxVertices, uint MaxSimplices, uint MaxAdjacencyBlocks) GetCapacities()
    {
        var maxV = _vertices.Capacity;
        var maxS = _simplices.Capacity;
        var maxB = _vertexAdjacency.ComputeTotalBlocks(maxV);

        return (maxV, maxS, maxB);
    }

    /// <summary>
    /// Writes a bit-exact binary snapshot of the vertex store, simplex store, and vertex-to-simplex adjacency map to <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The destination checkpoint stream.</param>
    /// <param name="maxVertices">The total vertex capacity of the arena.</param>
    /// <param name="maxSimplices">The total simplex capacity of the arena.</param>
    /// <param name="maxAdjacencyBlocks">The total adjacency block capacity of the arena.</param>
    public readonly void WriteCheckpoint(Stream stream, uint maxVertices, uint maxSimplices, uint maxAdjacencyBlocks)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _vertices.WriteCheckpoint(stream, maxVertices);
        _simplices.WriteCheckpoint(stream, maxSimplices);
        _vertexAdjacency.WriteCheckpoint(stream, maxAdjacencyBlocks, maxVertices);
    }

    /// <summary>
    /// Restores a bit-exact binary snapshot of the vertex store, simplex store, and vertex-to-simplex adjacency map from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The source checkpoint stream.</param>
    /// <param name="maxVertices">The total vertex capacity of the arena.</param>
    /// <param name="maxSimplices">The total simplex capacity of the arena.</param>
    /// <param name="maxAdjacencyBlocks">The total adjacency block capacity of the arena.</param>
    public void ReadCheckpoint(Stream stream, uint maxVertices, uint maxSimplices, uint maxAdjacencyBlocks)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _vertices.ReadCheckpoint(stream, maxVertices);
        _simplices.ReadCheckpoint(stream, maxSimplices);
        _vertexAdjacency.ReadCheckpoint(stream, maxAdjacencyBlocks, maxVertices);
    }
}