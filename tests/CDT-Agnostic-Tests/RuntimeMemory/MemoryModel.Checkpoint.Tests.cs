using CDT_Agnostic.RuntimeMemory;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

namespace CDT_Agnostic_Tests.RuntimeMemory;

public sealed unsafe class MemoryModelCheckpointTests
{
    [Fact]
    public void WriteAndReadCheckpoint_RoundTripsCapacitiesSlotsAndAdjacencyBitExact()
    {
        const uint maxV = 6;
        const uint maxS = 16;
        const uint maxB = 4;

        var source = new MemoryModel(maxV, maxS, maxB);
        var target = new MemoryModel(maxV, maxS, maxB);

        try
        {
            var v0 = source.AllocVertex();
            var v1 = source.AllocVertex();
            source.FreeVertex(v0); // Fragment vertex free-stack

            for (var i = 0; i < 15; i++)
            {
                var s = source.AllocSimplex();
                source.Link(v1, s); // Spans 2 AdjacencyBlock pages (14 + 1)
            }

            source.Unlink(v1, new SimplexId(3));
            source.FreeSimplex(new SimplexId(3));

            Assert.Equal((maxV, maxS, maxB), source.GetCapacities());

            using var stream = new MemoryStream();
            source.WriteCheckpoint(stream, maxV, maxS, maxB);

            stream.Position = 0;
            target.ReadCheckpoint(stream, maxV, maxS, maxB);

            Assert.Equal((maxV, maxS, maxB), target.GetCapacities());
            Assert.Equal(source.VertexCount, target.VertexCount);
            Assert.Equal(source.VertexFreeCount, target.VertexFreeCount);
            Assert.Equal(source.SimplexCount, target.SimplexCount);
            Assert.Equal(source.SimplexFreeCount, target.SimplexFreeCount);
            Assert.Equal(CollectIncident(in source, v1), CollectIncident(in target, v1));

            // Subsequent allocations must pop the exact same recycled IDs from the LIFO free-stacks
            Assert.Equal(source.AllocVertex(), target.AllocVertex());
            Assert.Equal(source.AllocSimplex(), target.AllocSimplex());
        }
        finally
        {
            source.Dispose();
            target.Dispose();
        }
    }

    [Fact]
    public void Checkpoint_WithNullStream_ThrowsArgumentNullException()
    {
        var model = new MemoryModel(maxVertices: 2, maxSimplices: 2, maxAdjacencyBlocks: 2);
        try
        {
            Assert.Throws<ArgumentNullException>(() => model.WriteCheckpoint(null!, 2, 2, 2));
            Assert.Throws<ArgumentNullException>(() => model.ReadCheckpoint(null!, 2, 2, 2));
        }
        finally
        {
            model.Dispose();
        }
    }

    private static List<int> CollectIncident(in MemoryModel model, VertexId vertex)
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