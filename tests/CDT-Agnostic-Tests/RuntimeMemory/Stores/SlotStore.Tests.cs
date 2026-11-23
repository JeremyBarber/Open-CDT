using CDT_Agnostic.RuntimeMemory.Stores;

using Xunit;

namespace CDT_Agnostic_Tests.RuntimeMemory.Stores;

public sealed class SlotStoreTests
{
    [Fact]
    public void Alloc_SeedsSequentiallyFromZero_AndThrowsWhenExhausted()
    {
        var store = new SlotStore(maxSlots: 4);
        try
        {
            Assert.Equal(0, store.Count);
            Assert.Equal(4, store.FreeCount);

            for (var expectedId = 0; expectedId < 4; expectedId++)
            {
                var id = store.Alloc();
                Assert.Equal(expectedId, id);
                Assert.Equal(expectedId + 1, store.Count);
                Assert.Equal(4 - (expectedId + 1), store.FreeCount);
                Assert.Equal(expectedId, store.GetAt(expectedId));
            }

            Assert.Throws<InvalidOperationException>(() => store.Alloc());
        }
        finally
        {
            store.Dispose();
        }
    }

    [Fact]
    public void Free_MaintainsDenseActiveListViaSwapWithLast_AndReusesIdsInLifoOrder()
    {
        var store = new SlotStore(maxSlots: 4);
        try
        {
            // Allocate 0, 1, 2, 3 -> _active = [0, 1, 2, 3]
            for (var i = 0; i < 4; i++)
                store.Alloc();

            // 1. Free interior slot 1 -> displaced tail slot 3 moves into pos 1: _active = [0, 3, 2]
            store.Free(1);
            Assert.Equal(3, store.Count);
            Assert.Equal(1, store.FreeCount);
            Assert.Equal([0, 3, 2], [store.GetAt(0), store.GetAt(1), store.GetAt(2)]);

            // 2. Now free slot 3 (which was just displaced to pos 1!).
            //    This proves _activeIndex[3] was updated to 1 during step 1: _active = [0, 2]
            store.Free(3);
            Assert.Equal(2, store.Count);
            Assert.Equal(2, store.FreeCount);
            Assert.Equal([0, 2], [store.GetAt(0), store.GetAt(1)]);

            // 3. Free the current tail element (slot 2 at pos 1, testing self-swap): _active = [0]
            store.Free(2);
            Assert.Equal(1, store.Count);
            Assert.Equal(3, store.FreeCount);
            Assert.Equal(0, store.GetAt(0));

            // 4. Re-allocating pops from the LIFO free-stack in reverse order of Free(): 2, then 3, then 1
            Assert.Equal(2, store.Alloc());
            Assert.Equal(3, store.Alloc());
            Assert.Equal(1, store.Alloc());
            Assert.Equal(4, store.Count);
            Assert.Equal(0, store.FreeCount);
        }
        finally
        {
            store.Dispose();
        }
    }
}