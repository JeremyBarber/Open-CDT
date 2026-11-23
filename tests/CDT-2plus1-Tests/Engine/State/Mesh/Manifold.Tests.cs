using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RuntimeMemory;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

namespace CDT_2plus1_Tests.Engine.State.Mesh;

public sealed unsafe class ManifoldTests
{
    [Fact]
    public void Manifold_AllocatesCorrectBoundsAndCacheAlignment_WithoutSegfaulting()
    {
        const uint maxV = 1000;
        const uint maxS = 5000;

        var m = Manifold.Create(maxVertices: maxV, maxSimplices: maxS, maxAdjacencyBlocks: 10);
        try
        {
            var raw = (ManifoldOverlay*)m;

            // Prove SimplexPool is locked to a 64-byte L1 cache-line boundary
            Assert.Equal(0UL, (ulong)raw->SimplexPool % (ulong)RuntimeConstants.CacheLineBytes);

            // Write and read back at the upper boundary of each arena buffer
            raw->SimplexPool[maxS - 1].NeighborSimplices[0] = 999;
            raw->SimplexTypes[maxS - 1] = 31;
            raw->VertexTimes[maxV - 1] = 42;

            Assert.Equal(999, raw->SimplexPool[maxS - 1].NeighborSimplices[0]);
            Assert.Equal(31, raw->SimplexTypes[maxS - 1]);
            Assert.Equal(42, raw->VertexTimes[maxV - 1]);
        }
        finally
        {
            Manifold.Destroy(ref m);
        }
    }

    [Fact]
    public void Manifold_Create_InitialisesMemoryToInvalidSentinels()
    {
        var m = Manifold.Create(maxVertices: 10, maxSimplices: 10, maxAdjacencyBlocks: 1);
        try
        {
            var raw = (ManifoldOverlay*)m;

            for (var i = 0; i < 10; i++)
            {
                Assert.Equal(RuntimeConstants.InvalidData, raw->VertexTimes[i]);
                Assert.Equal(RuntimeConstants.InvalidDataByte, raw->SimplexTypes[i]);

                ref var s = ref raw->SimplexPool[i];

                for (var slot = 0; slot < 4; slot++)
                {
                    Assert.Equal(SimplexId.Invalid.Value, s.Vertices[slot]);
                    Assert.Equal(SimplexId.Invalid.Value, s.NeighborSimplices[slot]);
                }
            }
        }
        finally
        {
            Manifold.Destroy(ref m);
        }
    }

    [Fact]
    public void Manifold_Create_ThrowsOnZeroDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Manifold.Create(maxVertices: 0, maxSimplices: 100, maxAdjacencyBlocks: 10));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Manifold.Create(maxVertices: 100, maxSimplices: 0, maxAdjacencyBlocks: 10));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Manifold.Create(maxVertices: 100, maxSimplices: 100, maxAdjacencyBlocks: 0));
    }

    [Fact]
    public void Manifold_Dispose_DoesNotLeakMemory()
    {
        // Warm up the allocator pools once before measuring
        var warmup = Manifold.Create(maxVertices: 500, maxSimplices: 1000, maxAdjacencyBlocks: 10);
        Manifold.Destroy(ref warmup);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();

        using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        currentProcess.Refresh();
        var initialMemory = currentProcess.PrivateMemorySize64;

        // 10,000 iterations allocates and frees ~800 MB of unmanaged memory.
        for (var i = 0; i < 10_000; i++)
        {
            var m = Manifold.Create(maxVertices: 500, maxSimplices: 1000, maxAdjacencyBlocks: 10);
            Manifold.Destroy(ref m);
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        currentProcess.Refresh();
        var finalMemory = currentProcess.PrivateMemorySize64;
        var leakedBytes = finalMemory - initialMemory;

        // Allow up to 20 MB of headroom for concurrent xUnit test threads (a real leak across 10,000 iterations is 20–800 MB).
        Assert.True(leakedBytes < 20_000_000, $"Memory leak detected! Leaked {leakedBytes} bytes.");
    }

    [Fact]
    public void Manifold_Dispose_CanBeCalledMultipleTimesSafely()
    {
        var m = Manifold.Create(10, 10, 10);

        Manifold.Destroy(ref m);
        Assert.Null(m);

        var exception = Record.Exception(() => Manifold.Destroy(ref m));
        Assert.Null(exception);
    }
}