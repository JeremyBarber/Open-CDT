using CDT_2plus1.Engine.Topology;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1_Tests.Engine.State.Mesh.Accessors;

public sealed unsafe class InspectingAccessorTests
{
    [Fact]
    public void Inspector_ReadsSimplexMemoryOffsetsCorrectly()
    {
        var m = Create(maxVertices: 10, maxSimplices: 10, maxAdjacencyBlocks: 10);
        try
        {
            var raw = (ManifoldOverlay*)m;
            var inspector = new InspectingAccessor(m);

            // 1. Manually write raw data to Simplex ID 5 via God-Mode overlay
            raw->SimplexPool[5].Vertices[0] = 111;
            raw->SimplexPool[5].Vertices[1] = 222;
            raw->SimplexPool[5].NeighborSimplices[2] = 333; // Face 2 points to Simplex 333
            raw->SimplexTypes[5] = (byte)SimplexType.ThreeOne;

            // 2. Assert the Inspector reads the correct offsets
            Assert.Equal(4, InspectingAccessor.FacetsPerSimplex);
            Assert.Equal(4, InspectingAccessor.VerticesPerSimplex);
            Assert.Equal(111, inspector.GetVertex(new SimplexId(5), 0).Value);
            Assert.Equal(222, inspector.GetVertex(new SimplexId(5), 1).Value);
            Assert.Equal(333, inspector.GetNeighbor(new SimplexId(5), 2).Value);

            Assert.True(inspector.SimplexIsType(new SimplexId(5), SimplexType.ThreeOne));
            Assert.False(inspector.SimplexIsType(new SimplexId(5), SimplexType.TwoTwo));
        }
        finally
        {
            Destroy(ref m);
        }
    }

    [Fact]
    public void Inspector_ReadsVertexTimeOffsetsCorrectly()
    {
        var m = Create(maxVertices: 20, maxSimplices: 10, maxAdjacencyBlocks: 10);
        try
        {
            var raw = (ManifoldOverlay*)m;
            var inspector = new InspectingAccessor(m);

            raw->VertexTimes[15] = 42;

            Assert.Equal(42, inspector.GetVertexTime(new VertexId(15)).Value);
        }
        finally
        {
            Destroy(ref m);
        }
    }

    [Fact]
    public void Inspector_ReadsTypeStoresCorrectly()
    {
        var m = Create(maxVertices: 10, maxSimplices: 100, maxAdjacencyBlocks: 10);
        try
        {
            var raw = (ManifoldOverlay*)m;
            var inspector = new InspectingAccessor(m);

            raw->TypeStore31.Add(new(10));
            raw->TypeStore31.Add(new(20));
            raw->TypeStore22.Add(new(99));
            raw->TypeStore13.Add(new(77));

            Assert.Equal(2, inspector.N31);
            Assert.Equal(1, inspector.N22);
            Assert.Equal(1, inspector.N13);

            var ids31 = inspector.ActiveIds31;
            Assert.Equal(10, ids31[0]);
            Assert.Equal(20, ids31[1]);
            Assert.Equal(99, inspector.ActiveIds22[0]);
            Assert.Equal(77, inspector.ActiveIds13[0]);
        }
        finally
        {
            Destroy(ref m);
        }
    }
}