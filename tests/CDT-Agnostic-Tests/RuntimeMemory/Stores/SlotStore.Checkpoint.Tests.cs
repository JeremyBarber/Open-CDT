using CDT_Agnostic.RuntimeMemory.Native;
using CDT_Agnostic.RuntimeMemory.Stores;

using Xunit;

namespace CDT_Agnostic_Tests.RuntimeMemory.Stores;

public sealed class SlotStoreCheckpointTests
{
    [Fact]
    public void WriteAndReadCheckpoint_PreservesActiveListAndLifoFreeStackOrder()
    {
        const uint capacity = 8;
        var source = new SlotStore(capacity);
        var target = new SlotStore(capacity);

        try
        {
            // Allocate 0, 1, 2, 3 and free 1 to fragment the LIFO stack
            _ = source.Alloc();
            var id1 = source.Alloc();
            _ = source.Alloc();
            _ = source.Alloc();
            source.Free(id1);

            Assert.Equal(capacity, source.Capacity);

            using var stream = new MemoryStream();
            source.WriteCheckpoint(stream, capacity);

            stream.Position = 0;
            target.ReadCheckpoint(stream, capacity);

            Assert.Equal(source.Count, target.Count);
            Assert.Equal(source.FreeCount, target.FreeCount);

            for (var i = 0; i < source.Count; i++)
                Assert.Equal(source.GetAt(i), target.GetAt(i));

            // Next allocation must pop the exact same recycled ID (id1 == 1) from the LIFO free-stack
            Assert.Equal(source.Alloc(), target.Alloc());
        }
        finally
        {
            source.Dispose();
            target.Dispose();
        }
    }

    [Fact]
    public void ReadCheckpoint_WhenCapacityConservationViolated_ThrowsInvalidDataException()
    {
        var store = new SlotStore(maxSlots: 8);
        try
        {
            using var corruptedStream = new MemoryStream();
            ArenaBinaryIO.WriteValue(corruptedStream, 5); // activeCount = 5
            ArenaBinaryIO.WriteValue(corruptedStream, 5); // freeHead = 5 (5 + 5 = 10 != 8)
            corruptedStream.Position = 0;

            Assert.Throws<InvalidDataException>(() => store.ReadCheckpoint(corruptedStream, expectedCapacity: 8));
        }
        finally
        {
            store.Dispose();
        }
    }

    [Fact]
    public void UninitializedStore_ThrowsInvalidOperationException()
    {
        using var stream = new MemoryStream();
        SlotStore uninitialized = default;

        Assert.Throws<InvalidOperationException>(() => uninitialized.WriteCheckpoint(stream, 8));
        Assert.Throws<InvalidOperationException>(() => uninitialized.ReadCheckpoint(stream, 8));
    }
}