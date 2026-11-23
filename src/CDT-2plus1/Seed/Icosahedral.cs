using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.RuntimeMemory.Identifiers;
using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.Topology.TopologyConstants;

namespace CDT_2plus1.Seed;

internal static unsafe class Icosahedral
{
    /// <summary>
    /// A zero-allocation payload holding the exact topological metrics for a subdivided universe.
    /// </summary>
    /// <param name="VerticesPerSlice">The total number of spatial vertices in a single S² time slice.</param>
    /// <param name="TrianglesPerSlice">The total number of spatial triangles in a single S² time slice.</param>
    /// <param name="SlabSimplexCount">The total number of 3D simplices in a single temporal slab (t to t+1).</param>
    /// <param name="TotalVertices">The absolute total volume of vertices across all timeslices.</param>
    /// <param name="TotalSimplices">The absolute total volume of simplices across all timeslices.</param>
    internal readonly record struct IcosahedralMetrics(
        uint VerticesPerSlice,
        uint TrianglesPerSlice,
        uint SlabSimplexCount,
        uint TotalVertices,
        uint TotalSimplices)
    {
        internal static IcosahedralMetrics Calculate(uint subdivisions, uint timeslices)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(subdivisions, (uint)1);
            ArgumentOutOfRangeException.ThrowIfLessThan(timeslices, (uint)3); // Minimal valid S¹ topology

            var trianglesPerSlice = 20 * subdivisions * subdivisions;
            var verticesPerSlice = 10 * subdivisions * subdivisions + 2;
            var slabSimplexCount = SimplicesPerPrism * trianglesPerSlice;

            return new IcosahedralMetrics(
                VerticesPerSlice: verticesPerSlice,
                TrianglesPerSlice: trianglesPerSlice,
                SlabSimplexCount: slabSimplexCount,
                TotalVertices: timeslices * verticesPerSlice,
                TotalSimplices: timeslices * slabSimplexCount
            );
        }
    }

    /// <summary>
    /// Predicts the exact topological size of the resulting universe so the Unmanaged Arena can allocate
    /// the precise contiguous memory blocks before the Big Bang occurs.
    /// </summary>
    internal static (uint Vertices, uint Simplices) CalculateIcosahedralSize(uint subdivisions, uint timeslices)
    {
        var metrics = IcosahedralMetrics.Calculate(subdivisions, timeslices);
        return (metrics.TotalVertices, metrics.TotalSimplices);
    }

    /// <summary>
    /// Constructs the periodic discrete spacetime manifold.
    /// </summary>
    /// <remarks>
    /// Mathematical properties of the generated seed:
    /// - Spatial slices (S²) contain V = 10k² + 2 vertices and F = 20k² triangles.
    /// - The manifold contains strictly N = 3 × T × 20k² tetrahedra.
    /// - Every interior vertex has spatial degree exactly 6 (12 pole vertices have degree 5).
    /// - Time boundaries are strictly periodic (t_max wraps to t_0).
    /// </remarks>
    internal static void BuildIcosahedral(SeederAccessor universe, uint subdivisions, uint timeslices)
    {
        // =====================================================================================
        // 1. GEOMETRIC PROJECTION
        // =====================================================================================
        var metrics = IcosahedralMetrics.Calculate(subdivisions, timeslices);
        var (_, triangles) = GeodesicSphere.Build(subdivisions);

        // =====================================================================================
        // 2. VERTEX ALLOCATION
        // =====================================================================================
        var vertexMap = new VertexId[timeslices * metrics.VerticesPerSlice];
        for (var t = 0; t < timeslices; t++)
        {
            for (var v = 0; v < metrics.VerticesPerSlice; v++)
            {
                vertexMap[t * metrics.VerticesPerSlice + v] = universe.SeedVertex(new(t));
            }
        }

        // =====================================================================================
        // 3. 3D EXTRUSION (S² × S¹)
        // =====================================================================================
        // Extrude the 2D triangles into 3D spacetime slabs (31s, 22s, 13s)
        var slabSimplexIds = new SimplexId[timeslices * metrics.SlabSimplexCount];

        for (var t = 0; t < timeslices; t++)
        {
            var tNext = (t + 1) % (int)timeslices; // Periodic boundary condition (The S¹ loop)
            var slabBase = t * (int)metrics.SlabSimplexCount;

            for (var i = 0; i < metrics.TrianglesPerSlice; i++)
            {
                (var a, var b, var c) = triangles[i];

                // Standardize edge winding by sorting the spatial vertex tuple (A < B < C).
                // This ensures neighboring slabs resolve facet sharing predictably.
                if (a > b) (a, b) = (b, a);
                if (b > c) (b, c) = (c, b);
                if (a > b) (a, b) = (b, a);

                var as0 = vertexMap[t * metrics.VerticesPerSlice + a];
                var bs0 = vertexMap[t * metrics.VerticesPerSlice + b];
                var cs0 = vertexMap[t * metrics.VerticesPerSlice + c];

                var as1 = vertexMap[tNext * metrics.VerticesPerSlice + a];
                var bs1 = vertexMap[tNext * metrics.VerticesPerSlice + b];
                var cs1 = vertexMap[tNext * metrics.VerticesPerSlice + c];

                // A single triangular prism is sliced into 3 valid CDT tetrahedra.
                var t31 = universe.SeedSimplex(as0, bs0, cs0, as1, SimplexType.ThreeOne);
                var t22 = universe.SeedSimplex(bs0, cs0, as1, bs1, SimplexType.TwoTwo);
                var t13 = universe.SeedSimplex(cs0, as1, bs1, cs1, SimplexType.OneThree);

                var offset = i * SimplicesPerPrism;
                slabSimplexIds[slabBase + offset + 0] = t31;
                slabSimplexIds[slabBase + offset + 1] = t22;
                slabSimplexIds[slabBase + offset + 2] = t13;
            }
        }

        // =====================================================================================
        // 4. TOPOLOGICAL STITCHING
        // =====================================================================================
        WireNeighbors(universe, slabSimplexIds);
        FixGlobalOrientation(universe, slabSimplexIds);
    }

    /// <summary>
    /// Matches shared 3-vertex facets across the entire manifold to establish the neighborhood graph.
    /// </summary>
    private static void WireNeighbors(SeederAccessor universe, SimplexId[] simplexIds)
    {
        var openFacets = new Dictionary<(VertexId, VertexId, VertexId), (SimplexId sid, LocalFacetSlot slot)>(simplexIds.Length * FacetsPerSimplex);

        for (var i = 0; i < simplexIds.Length; i++)
        {
            var sid = simplexIds[i];
            ref readonly var simplex = ref universe.GetSimplex(sid);

            for (byte slot = 0; slot < FacetsPerSimplex; slot++)
            {
                LocalFacetSlot localSlot = new(slot);
                var key = SortedFacet(in simplex, localSlot);

                // If the facet already exists in the dictionary, we've found our neighbor!
                if (openFacets.Remove(key, out var neighbor))
                {
                    universe.SeedNeighbors(sid, localSlot, neighbor.sid, neighbor.slot);
                }
                else
                {
                    openFacets[key] = (sid, localSlot);
                }
            }
        }

        if (openFacets.Count != 0)
            throw new InvalidOperationException($"Seed triangulation failed: {openFacets.Count} facets remain unmatched (boundary holes detected).");
    }

    /// <summary>
    /// Corrects degenerate topological parity by enforcing consistent right-handed vertex winding.
    /// Uses Breadth-First Search (BFS) to propagate 'True North' from the first simplex to the entire manifold.
    /// </summary>
    private static void FixGlobalOrientation(SeederAccessor universe, SimplexId[] simplexIds)
    {
        if (simplexIds.Length == 0)
            return;

        var visited = new HashSet<SimplexId>();
        var queue = new Queue<SimplexId>();

        var startId = simplexIds[0];
        queue.Enqueue(startId);
        visited.Add(startId);

        while (queue.TryDequeue(out var idA))
        {
            ref readonly var sA = ref universe.GetSimplex(idA);

            for (byte faceA = 0; faceA < FacetsPerSimplex; faceA++)
            {
                SimplexId idB = new(sA.NeighborSimplices[faceA]);

                if (idB == SimplexId.Invalid || !visited.Add(idB))
                    continue;

                ref readonly var sB = ref universe.GetSimplex(idB);

                VertexId v1 = new(sA.Vertices[(faceA + 1) % VerticesPerSimplex]);
                VertexId v2 = new(sA.Vertices[(faceA + 2) % VerticesPerSimplex]);
                VertexId v3 = new(sA.Vertices[(faceA + 3) % VerticesPerSimplex]);

                var slot1 = sB.FindVertexSlot(v1);
                var slot2 = sB.FindVertexSlot(v2);
                var slot3 = sB.FindVertexSlot(v3);

                var faceB = EdgesPerSimplex - (slot1.Value + slot2.Value + slot3.Value);

                var signA = (faceA % 2 == 0) ? 1 : -1;
                var signB = (faceB % 2 == 0) ? 1 : -1;

                var inversions = 0;
                if (slot1.Value > slot2.Value) inversions++;
                if (slot1.Value > slot3.Value) inversions++;
                if (slot2.Value > slot3.Value) inversions++;

                var parity = (inversions % 2 == 0) ? 1 : -1;

                // Mutate the unmanaged arena directly if parity is inverted
                if ((signA * signB * parity) != -1)
                {
                    universe.InvertOrientation(idB);
                }

                queue.Enqueue(idB);
            }
        }
    }

    /// <summary>
    /// Helper method to extract the three vertices of a local face and sort them by ID.
    /// This provides a consistent hashing key for dictionary matching.
    /// </summary>
    private static (VertexId, VertexId, VertexId) SortedFacet(ref readonly Tetrahedron simplex, LocalFacetSlot oppositeSlot)
    {
        var v0 = VertexId.Invalid;
        var v1 = VertexId.Invalid;
        var v2 = VertexId.Invalid;

        for (byte k = 0; k < VerticesPerSimplex; k++)
        {
            if (k == oppositeSlot.Value)
                continue;

            VertexId v = new(simplex.Vertices[k]);

            if (v0 == VertexId.Invalid)
                v0 = v;
            else if (v1 == VertexId.Invalid)
                v1 = v;
            else
                v2 = v;
        }

        // Inline bubble-sort for exactly 3 elements
        if (v0.Value > v1.Value)
            (v0, v1) = (v1, v0);

        if (v1.Value > v2.Value)
            (v1, v2) = (v2, v1);

        if (v0.Value > v1.Value)
            (v0, v1) = (v1, v0);

        return (v0, v1, v2);
    }
}