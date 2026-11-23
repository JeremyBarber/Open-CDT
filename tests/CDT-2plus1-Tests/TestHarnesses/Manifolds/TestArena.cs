using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.Topology;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_2plus1_Tests.TestHarnesses.Manifolds;

/// <summary>
/// Shared allocation, disposal, and reciprocal boundary-padding utilities for isolated Pachner move test arenas.
/// </summary>
internal static unsafe class TestArena
{
    private const int FirstDummyBoundaryId = 50;

    public static Manifold* AllocateEmpty() =>
        Create(maxVertices: 100, maxSimplices: 100, maxAdjacencyBlocks: 100);

    public static void Free(Manifold* m) => Destroy(ref m);

    /// <summary>
    /// Scans all active simplices in <paramref name="m"/> and wires any unlinked boundary facets (<c>-1</c>)
    /// to unique dummy simplices (<c>50, 51, ...</c>) with reciprocal back-pointers on facet slot <c>0</c>.
    /// Guarantees that <c>PerformMove</c> never reads <c>SimplexPool[-1]</c> or fails <c>GetFacetFacingSimplexIndex</c>.
    /// </summary>
    public static void PadExternalBoundaries(Manifold* m)
    {
        var raw = (ManifoldOverlay*)m;
        var inspector = new InspectingAccessor(m);

        var padId = FirstDummyBoundaryId;
        var activeCount = inspector.Adjacency.TotalSimplices;

        for (var i = 0; i < activeCount; i++)
        {
            var sId = inspector.Adjacency.GetNthSimplexId(i).Value;
            ref var s = ref raw->SimplexPool[sId];

            for (var face = 0; face < TopologyConstants.FacetsPerSimplex; face++)
            {
                if (s.NeighborSimplices[face] == InvalidData)
                {
                    var dummyId = padId++;
                    s.NeighborSimplices[face] = dummyId;
                    raw->SimplexPool[dummyId].NeighborSimplices[0] = sId;
                }
            }
        }
    }
}