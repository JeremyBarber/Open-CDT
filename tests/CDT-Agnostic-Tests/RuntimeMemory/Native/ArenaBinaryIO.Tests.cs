using CDT_Agnostic.RuntimeMemory.Native;

using Xunit;

namespace CDT_Agnostic_Tests.RuntimeMemory.Native;

public sealed unsafe class ArenaBinaryIOTests
{
    [Fact]
    public void WriteAndReadValue_RoundTripsUnmanagedStructBitExact()
    {
        using var stream = new MemoryStream();
        var expected = 0xCAFEBABE_DEADBEEFUL;

        ArenaBinaryIO.WriteValue(stream, in expected);
        stream.Position = 0;

        var actual = ArenaBinaryIO.ReadValue<ulong>(stream);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WriteAndReadBlit_RoundTripsUnmanagedBufferBitExact()
    {
        const uint count = 16;
        var source = Allocation.Allocate<int>(count);
        var target = Allocation.Allocate<int>(count);

        try
        {
            for (var i = 0; i < count; i++)
                source[i] = (i * 10) - 50;

            using var stream = new MemoryStream();
            ArenaBinaryIO.WriteBlit(stream, source, count);

            stream.Position = 0;
            ArenaBinaryIO.ReadBlit(stream, target, count);

            for (var i = 0; i < count; i++)
                Assert.Equal(source[i], target[i]);
        }
        finally
        {
            Allocation.SafeFree(source);
            Allocation.SafeFree(target);
        }
    }

    [Fact]
    public void NullStreamOrNullBuffer_ThrowsExpectedException()
    {
        using var stream = new MemoryStream();
        var dummy = 42;
        int* nullPtr = null;

        Assert.Throws<ArgumentNullException>(() => ArenaBinaryIO.WriteValue(null!, in dummy));
        Assert.Throws<ArgumentNullException>(() => ArenaBinaryIO.ReadValue<int>(null!));
        Assert.Throws<InvalidOperationException>(() => ArenaBinaryIO.WriteBlit(stream, nullPtr, count: 4));
        Assert.Throws<InvalidOperationException>(() => ArenaBinaryIO.ReadBlit(stream, nullPtr, count: 4));
    }
}