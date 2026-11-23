using CDT_Agnostic.RandomNumberGeneration;
using CDT_Agnostic.RuntimeMemory.Native;

using Xunit;

namespace CDT_Agnostic_Tests.RandomNumberGeneration;

public sealed class Pcg32CheckpointTests
{
    [Fact]
    public void WriteAndReadCheckpoint_PreservesExactFutureRandomSequence()
    {
        Pcg32 source = default;
        source.Seed(initState: 987654321UL, initInc: 77UL);

        for (var i = 0; i < 15; i++)
            _ = source.NextUInt32();

        using var stream = new MemoryStream();
        source.WriteCheckpoint(stream);

        stream.Position = 0;
        Pcg32 restored = default;
        restored.ReadCheckpoint(stream);

        for (var i = 0; i < 64; i++)
        {
            Assert.Equal(source.NextUInt32(), restored.NextUInt32());
        }
    }

    [Fact]
    public void ReadCheckpoint_WithEvenIncrement_ThrowsInvalidDataException()
    {
        using var corruptedStream = new MemoryStream();
        ArenaBinaryIO.WriteValue(corruptedStream, 42UL);
        ArenaBinaryIO.WriteValue(corruptedStream, 100UL); // Violates Hull-Dobell odd-increment rule
        corruptedStream.Position = 0;

        Pcg32 rng = default;
        Assert.Throws<InvalidDataException>(() => rng.ReadCheckpoint(corruptedStream));
    }

    [Fact]
    public void Checkpoint_WithNullStream_ThrowsArgumentNullException()
    {
        Pcg32 rng = default;
        rng.Seed(1UL, 1UL);

        Assert.Throws<ArgumentNullException>(() => rng.WriteCheckpoint(null!));
        Assert.Throws<ArgumentNullException>(() => rng.ReadCheckpoint(null!));
    }
}