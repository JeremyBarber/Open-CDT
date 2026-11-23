using CDT_Agnostic.RuntimeMemory;
using CDT_Agnostic.RuntimeMemory.Adjacency;

using Xunit;

namespace CDT_Agnostic_Tests.RuntimeMemory.Adjacency;

public sealed unsafe class AdjacencyMapTests
{
    [Fact]
    public void AdjacencyBlock_Size_IsExactlyOne64ByteCacheLine()
    {
        Assert.Equal(RuntimeConstants.CacheLineBytes, sizeof(AdjacencyBlock));
        Assert.Equal(14, AdjacencyBlock.Capacity);
    }

    [Fact]
    public void SinglePage_AddEnumerateAndRemove_MaintainsSetAndRecyclesBlockWhenEmpty()
    {
        var map = new AdjacencyMap(maxBlocks: 1, maxEntities: 2);
        try
        {
            // Removing from an uninitialized entity or missing ID must be a safe no-op
            map.Remove(entityId: 0, id: 99);

            map.Add(entityId: 0, id: 10);
            map.Add(entityId: 0, id: 20);
            map.Add(entityId: 0, id: 30);

            // Removing an ID not in the page leaves existing IDs intact
            map.Remove(entityId: 0, id: 999);
            Assert.Equal([10, 20, 30], CollectIds(in map, entityId: 0));

            // Remove middle element (swap-and-pop replaces 20 with 30)
            map.Remove(entityId: 0, id: 20);
            Assert.Equal([10, 30], CollectIds(in map, entityId: 0));

            // Drain remaining elements so the single block is returned to the pool
            map.Remove(entityId: 0, id: 10);
            map.Remove(entityId: 0, id: 30);
            Assert.Empty(CollectIds(in map, entityId: 0));

            // Because maxBlocks == 1, entity 1 can only allocate a block if entity 0 returned its block!
            map.Add(entityId: 1, id: 777);
            Assert.Equal([777], CollectIds(in map, entityId: 1));
        }
        finally
        {
            map.Dispose();
        }
    }

    [Fact]
    public void MultiPage_CrossPageSwapAndPop_KeepsInteriorPagesFullAndRecyclesAllBlocks()
    {
        // 3 blocks * 14 capacity = 42 max items. We insert 35 items (14 + 14 + 7 across 3 pages).
        var map = new AdjacencyMap(maxBlocks: 3, maxEntities: 2);
        try
        {
            var expected = new HashSet<int>();
            for (var id = 100; id < 135; id++)
            {
                map.Add(entityId: 0, id);
                expected.Add(id);
            }

            // Removing a missing ID across a 3-page chain must be a safe no-op
            map.Remove(entityId: 0, id: 9999);

            // Remove an item from the Head page (100), middle page (115), and Tail page (134)
            foreach (var toRemove in new[] { 100, 115, 134 })
            {
                map.Remove(entityId: 0, toRemove);
                expected.Remove(toRemove);
            }

            var current = CollectIds(in map, entityId: 0);
            Assert.Equal(expected.Count, current.Count);
            Assert.Equal(expected, current.ToHashSet());

            // Now drain all remaining items from the front of the chain, forcing the 3rd page
            // and 2nd page to empty and re-anchor the tail pointer across multi-hop predecessor walks.
            foreach (var remainingId in current)
            {
                map.Remove(entityId: 0, remainingId);
            }

            Assert.Empty(CollectIds(in map, entityId: 0));

            // Prove all 3 blocks were returned to the pool by allocating 3 full pages on entity 1
            for (var id = 0; id < 3 * AdjacencyBlock.Capacity; id++)
            {
                map.Add(entityId: 1, id);
            }

            Assert.Equal(42, CollectIds(in map, entityId: 1).Count);
        }
        finally
        {
            map.Dispose();
        }
    }

    [Fact]
    public void Add_WhenBlockPoolExhausted_ThrowsInvalidOperationException()
    {
        var map = new AdjacencyMap(maxBlocks: 1, maxEntities: 1);
        try
        {
            for (var i = 0; i < AdjacencyBlock.Capacity; i++)
            {
                map.Add(entityId: 0, id: i);
            }

            // The 15th item requires a 2nd block, which exceeds maxBlocks (1)
            Assert.Throws<InvalidOperationException>(() => map.Add(entityId: 0, id: 999));
        }
        finally
        {
            map.Dispose();
        }
    }

    private static List<int> CollectIds(in AdjacencyMap map, int entityId)
    {
        var result = new List<int>();
        foreach (var page in map.GetEnumerator(entityId))
        {
            for (var i = 0; i < page.Count; i++)
            {
                result.Add(page.Data[i]);
            }
        }
        return result;
    }
}