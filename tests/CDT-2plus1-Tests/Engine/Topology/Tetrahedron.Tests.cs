using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

namespace CDT_2plus1_Tests.Engine.Topology;

public sealed unsafe class TetrahedronTests
{
    [Fact]
    public void FindVertexSlot_UsesSimdToLocateAllFourSlotsAndReturns32OnMiss()
    {
        Tetrahedron t = default;
        t.Vertices[0] = 10;
        t.Vertices[1] = 20;
        t.Vertices[2] = 30;
        t.Vertices[3] = 40;

        ref readonly var ro = ref t;

        Assert.Equal(0, ro.FindVertexSlot(new VertexId(10)).Value);
        Assert.Equal(1, ro.FindVertexSlot(new VertexId(20)).Value);
        Assert.Equal(2, ro.FindVertexSlot(new VertexId(30)).Value);
        Assert.Equal(3, ro.FindVertexSlot(new VertexId(40)).Value);
        Assert.Equal(32, ro.FindVertexSlot(new VertexId(99)).Value);
    }

    [Fact]
    public void GetFacetFacingSimplexIndex_UsesSimdToLocateAllFourFacetsAndReturns32OnMiss()
    {
        Tetrahedron t = default;
        t.NeighborSimplices[0] = 100;
        t.NeighborSimplices[1] = 200;
        t.NeighborSimplices[2] = 300;
        t.NeighborSimplices[3] = 400;

        ref readonly var ro = ref t;

        Assert.Equal(0, ro.GetFacetFacingSimplexIndex(new SimplexId(100)).Value);
        Assert.Equal(1, ro.GetFacetFacingSimplexIndex(new SimplexId(200)).Value);
        Assert.Equal(2, ro.GetFacetFacingSimplexIndex(new SimplexId(300)).Value);
        Assert.Equal(3, ro.GetFacetFacingSimplexIndex(new SimplexId(400)).Value);
        Assert.Equal(32, ro.GetFacetFacingSimplexIndex(new SimplexId(999)).Value);
    }

    [Fact]
    public void GetEdgeVertexSlots_And_GetNonEdgeVertices_CoverAllSixEdgesComplementary()
    {
        Tetrahedron t = default;
        t.Vertices[0] = 10;
        t.Vertices[1] = 20;
        t.Vertices[2] = 30;
        t.Vertices[3] = 40;

        ref readonly var ro = ref t;

        for (byte e = 0; e < TopologyConstants.EdgesPerSimplex; e++)
        {
            (var slotA, var slotB) = Tetrahedron.GetEdgeVertexSlots(new LocalEdgeSlot(e));
            Assert.NotEqual(slotA, slotB);

            var vA = new VertexId(ro.Vertices[slotA.Value]);
            var vB = new VertexId(ro.Vertices[slotB.Value]);

            (var x, var y) = ro.GetNonEdgeVertices(vA, vB);

            // All 4 vertices must sum to 10 + 20 + 30 + 40 = 100
            Assert.NotEqual(x, y);
            Assert.NotEqual(vA, x);
            Assert.NotEqual(vB, x);
            Assert.NotEqual(vA, y);
            Assert.NotEqual(vB, y);
            Assert.Equal(100, vA.Value + vB.Value + x.Value + y.Value);
        }
    }
}