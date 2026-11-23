using CDT_Agnostic.RuntimeMemory.Adjacency;
using CDT_Agnostic.RuntimeMemory.Native;

using Xunit;

namespace CDT_Agnostic_Tests.RuntimeMemory.Adjacency;

public sealed unsafe class AdjacencyMapCheckpointTests
{
    [Fact]
    public void WriteAndReadCheckpoint_PreservesMultiPageChainsAndTotalBlockCount()
    {
        const uint maxBlocks = 4;
        const uint maxEntities = 3;

        var source = new AdjacencyMap(maxBlocks, maxEntities);
        var target = new AdjacencyMap(maxBlocks, maxEntities);

        try
        {
            // Add 18 IDs to entity 0 (spanning 2 blocks: 14 + 4) and remove 1 to trigger swap-and-pop
            for (var id = 100; id < 118; id++)
                source.Add(entityId: 0, id);

            source.Remove(entityId: 0, id: 102);

            Assert.Equal(maxBlocks, source.ComputeTotalBlocks(maxEntities));

            using var stream = new MemoryStream();
            source.WriteCheckpoint(stream, maxBlocks, maxEntities);

            stream.Position = 0;
            target.ReadCheckpoint(stream, maxBlocks, maxEntities);

            Assert.Equal(maxBlocks, target.ComputeTotalBlocks(maxEntities));
            Assert.Equal(CollectIds(in source, 0), CollectIds(in target, 0));
        }
        finally
        {
            source.Dispose();
            target.Dispose();
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void ReadCheckpoint_WithOutOfBoundsFreeHead_ThrowsInvalidDataException(int invalidFreeHead)
    {
        var map = new AdjacencyMap(maxBlocks: 4, maxEntities: 2);
        try
        {
            using var corruptedStream = new MemoryStream();
            ArenaBinaryIO.WriteValue(corruptedStream, invalidFreeHead);
            corruptedStream.Position = 0;

            Assert.Throws<InvalidDataException>(() => map.ReadCheckpoint(corruptedStream, maxBlocks: 4, maxEntities: 2));
        }
        finally
        {
            map.Dispose();
        }
    }

    [Fact]
    public void UninitializedMap_ThrowsInvalidOperationException()
    {
        using var stream = new MemoryStream();
        AdjacencyMap uninitialized = default;

        Assert.Throws<InvalidOperationException>(() => uninitialized.ComputeTotalBlocks(4));
        Assert.Throws<InvalidOperationException>(() => uninitialized.WriteCheckpoint(stream, 4, 4));
        Assert.Throws<InvalidOperationException>(() => uninitialized.ReadCheckpoint(stream, 4, 4));
    }

    private static List<int> CollectIds(in AdjacencyMap map, int entityId)
    {
        var list = new List<int>();
        foreach (var page in map.GetEnumerator(entityId))
        {
            for (var i = 0; i < page.Count; i++)
                list.Add(page.Data[i]);
        }
        return list;
    }
}