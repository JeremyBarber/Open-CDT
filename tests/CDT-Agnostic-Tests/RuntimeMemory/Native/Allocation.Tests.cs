using CDT_Agnostic.RuntimeMemory;
using CDT_Agnostic.RuntimeMemory.Native;

using Xunit;

namespace CDT_Agnostic_Tests.RuntimeMemory.Native;

public sealed unsafe class AllocationTests
{
    [Fact]
    public void AllocateAndFill_ZeroesMemoryAndBroadcastsTwoComplementMinusOne()
    {
        const uint count = 32;
        var buffer = Allocation.Allocate<int>(count);

        try
        {
            Assert.NotEqual(nint.Zero, (nint)buffer);

            // 1. Allocate must zero-initialize every element
            for (uint i = 0; i < count; i++)
                Assert.Equal(0, buffer[i]);

            // 2. Default Fill (0xFF) must set every 32-bit int to -1 (InvalidData)
            Allocation.Fill(buffer, count);
            for (uint i = 0; i < count; i++)
                Assert.Equal(RuntimeConstants.InvalidData, buffer[i]);
        }
        finally
        {
            Allocation.SafeFree(buffer);
        }
    }

    [Fact]
    public void AllocateAligned_AlignsTo64ByteCacheLineAndZeroesBuffer()
    {
        const uint count = 19; // Odd element count (76 bytes -> padded to 128 bytes)
        var aligned = Allocation.AllocateAligned<int>(count);

        try
        {
            Assert.NotEqual(nint.Zero, (nint)aligned);
            Assert.Equal(0UL, (ulong)aligned % RuntimeConstants.CacheLineBytes);

            for (uint i = 0; i < count; i++)
                Assert.Equal(0, aligned[i]);
        }
        finally
        {
            Allocation.SafeFreeAligned(aligned);
        }
    }

    [Fact]
    public void NullPointers_InFillAndSafeFree_AreSafeNoOps()
    {
        int* nullPtr = null;

        Allocation.Fill(nullPtr, capacity: 10);
        Allocation.SafeFree(nullPtr);
        Allocation.SafeFreeAligned(nullPtr);
    }
}