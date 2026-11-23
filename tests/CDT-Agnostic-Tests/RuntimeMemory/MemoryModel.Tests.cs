using CDT_Agnostic.RuntimeMemory;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

namespace CDT_Agnostic_Tests.RuntimeMemory;

public sealed unsafe class MemoryModelTests
{
    [Fact]
    public void VertexAndSimplexLifecycle_TracksCountsAndRecyclesIds()
    {
        var model = new MemoryModel(maxVertices: 4, maxSimplices: 6, maxAdjacencyBlocks: 4);
        try
        {
            Assert.Equal(0, model.VertexCount);
            Assert.Equal(4, model.VertexFreeCount);
            Assert.Equal(0, model.SimplexCount);
            Assert.Equal(6, model.SimplexFreeCount);

            var v0 = model.AllocVertex();
            var v1 = model.AllocVertex();
            Assert.Equal(2, model.VertexCount);
            Assert.Equal(2, model.VertexFreeCount);
            Assert.Equal(v0, model.GetVertexAt(0));
            Assert.Equal(v1, model.GetVertexAt(1));

            model.FreeVertex(v0);
            Assert.Equal(1, model.VertexCount);
            Assert.Equal(3, model.VertexFreeCount);
            Assert.Equal(v1, model.GetVertexAt(0));

            var s0 = model.AllocSimplex();
            var s1 = model.AllocSimplex();
            Assert.Equal(2, model.SimplexCount);
            Assert.Equal(4, model.SimplexFreeCount);
            Assert.Equal(s0, model.GetSimplexAt(0));
            Assert.Equal(s1, model.GetSimplexAt(1));

            model.FreeSimplex(s0);
            Assert.Equal(1, model.SimplexCount);
            Assert.Equal(5, model.SimplexFreeCount);
            Assert.Equal(s1, model.GetSimplexAt(0));
        }
        finally
        {
            model.Dispose();
        }
    }

    [Fact]
    public void LinkAndUnlink_UpdatesIncidentPagesForVertex()
    {
        var model = new MemoryModel(maxVertices: 2, maxSimplices: 4, maxAdjacencyBlocks: 2);
        try
        {
            var v0 = model.AllocVertex();
            var s0 = model.AllocSimplex();
            var s1 = model.AllocSimplex();

            model.Link(v0, s0);
            model.Link(v0, s1);

            Assert.Equal([s0.Value, s1.Value], CollectIncidentSimplices(in model, v0));

            model.Unlink(v0, s0);
            Assert.Equal([s1.Value], CollectIncidentSimplices(in model, v0));
        }
        finally
        {
            model.Dispose();
        }
    }

    private static List<int> CollectIncidentSimplices(in MemoryModel model, VertexId vertex)
    {
        var list = new List<int>();
        foreach (var page in model.GetIncidentPages(vertex))
        {
            for (var i = 0; i < page.Count; i++)
                list.Add(page.Data[i]);
        }
        return list;
    }
}