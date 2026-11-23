using CDT_Agnostic.RuntimeMemory.Identifiers;
using CDT_Agnostic.RuntimeMemory.Native;
using CDT_Agnostic.RuntimeMemory.Stores;

using Xunit;

namespace CDT_Agnostic_Tests.RuntimeMemory.Stores;

public sealed class TypeStoreCheckpointTests
{
    [Fact]
    public void WriteAndReadCheckpoint_PreservesExactDensePermutationAndInversePositions()
    {
        const uint maxIds = 32;
        var source = new TypeStore(maxIds);
        var target = new TypeStore(maxIds);

        try
        {
            source.Add(new SimplexId(3));
            source.Add(new SimplexId(14));
            source.Add(new SimplexId(27));
            source.Remove(new SimplexId(14)); // Displaces 27 into slot 1

            using var stream = new MemoryStream();
            source.WriteCheckpoint(stream, maxIds);

            stream.Position = 0;
            target.ReadCheckpoint(stream, maxIds);

            Assert.Equal(source.IdsOfType.ToArray(), target.IdsOfType.ToArray());

            // Removing the displaced ID (27) from the restored store proves _position[27] was preserved
            target.Remove(new SimplexId(27));
            Assert.Equal([3], target.IdsOfType.ToArray());
        }
        finally
        {
            source.Dispose();
            target.Dispose();
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void ReadCheckpoint_WithOutOfBoundsCount_ThrowsInvalidDataException(int invalidCount)
    {
        var store = new TypeStore(maxIds: 8);
        try
        {
            using var corruptedStream = new MemoryStream();
            ArenaBinaryIO.WriteValue(corruptedStream, invalidCount);
            corruptedStream.Position = 0;

            Assert.Throws<InvalidDataException>(() => store.ReadCheckpoint(corruptedStream, maxIds: 8));
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
        TypeStore uninitialized = default;

        Assert.Throws<InvalidOperationException>(() => uninitialized.WriteCheckpoint(stream, 8));
        Assert.Throws<InvalidOperationException>(() => uninitialized.ReadCheckpoint(stream, 8));
    }
}