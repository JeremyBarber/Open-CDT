namespace CDT_2plus1.Engine.Topology;

/// <summary>
/// Compile-time topological constants for the 2+1D simplicial triangulation.
/// In 2+1D, the top-dimensional building block is a tetrahedron (<c>3</c>-simplex)
/// with 4 vertices, 4 triangular facets (<c>2</c>-simplices), and 6 edges (<c>C(4,2) = 6</c>).
/// </summary>
internal static class TopologyConstants
{
    /// <summary>The number of vertices in a single tetrahedron (<c>4</c>).</summary>
    public const int VerticesPerSimplex = 4;

    /// <summary>The number of triangular boundary facets in a single tetrahedron (<c>4</c>).</summary>
    public const int FacetsPerSimplex = 4;

    /// <summary>The number of edges in a single tetrahedron (<c>6</c>).</summary>
    public const int EdgesPerSimplex = 6;

    /// <summary>The number of tetrahedra required to triangulate a single extruded triangular prism (<c>3</c>).</summary>
    public const int SimplicesPerPrism = 3;
}