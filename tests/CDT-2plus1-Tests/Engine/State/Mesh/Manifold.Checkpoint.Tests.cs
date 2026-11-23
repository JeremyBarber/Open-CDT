using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.Topology;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RuntimeMemory.Identifiers;
using CDT_Agnostic.RuntimeMemory.Native;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1_Tests.Engine.State.Mesh;

public sealed unsafe class ManifoldCheckpointTests
{
    [Fact]
    public void WriteAndReadCheckpoint_RoundTripsPoolsTimeslicesAndTypeStoresBitExact()
    {
        var source = Create(maxVertices: 16, maxSimplices: 32, maxAdjacencyBlocks: 16);
        Manifold* restored = null;

        try
        {
            var mutator = new MutatingAccessor(source);
            var v0 = mutator.AddVertex(new TimeSlice(0));
            var v1 = mutator.AddVertex(new TimeSlice(0));
            var v2 = mutator.AddVertex(new TimeSlice(0));
            var v3 = mutator.AddVertex(new TimeSlice(1));
            var v4 = mutator.AddVertex(new TimeSlice(1));

            var s31 = mutator.RegisterSimplex(v0, v1, v2, v3, SimplexType.ThreeOne);
            var s22 = mutator.RegisterSimplex(v0, v1, v3, v4, SimplexType.TwoTwo);
            mutator.LinkNeighbors(s31, new LocalFacetSlot(2), s22, new LocalFacetSlot(3));

            using var stream = new MemoryStream();
            source->WriteCheckpoint(stream);

            stream.Position = 0;
            restored = ReadCheckpoint(stream);

            Assert.Equal(32u, restored->MaxSimplices);

            var inspector = new InspectingAccessor(restored);
            Assert.Equal(2, inspector.Adjacency.TotalSimplices);
            Assert.Equal(5, inspector.Adjacency.TotalVertices);
            Assert.Equal(1, inspector.N31);
            Assert.Equal(1, inspector.N22);
            Assert.Equal(0, inspector.N13);
            Assert.Equal(s22, inspector.GetNeighbor(s31, 2));
            Assert.Equal(s31, inspector.GetNeighbor(s22, 3));
            Assert.Equal(1, inspector.GetVertexTime(v3).Value);
        }
        finally
        {
            Destroy(ref source);
            Destroy(ref restored);
        }
    }

    [Fact]
    public void WriteCheckpoint_OnDefaultOrNullStream_ThrowsExpectedException()
    {
        Manifold uninitialized = default;
        using var stream = new MemoryStream();

        Assert.Throws<ArgumentNullException>(() => uninitialized.WriteCheckpoint(null!));
        Assert.Throws<InvalidOperationException>(() => uninitialized.WriteCheckpoint(stream));
    }

    [Fact]
    public void ReadCheckpoint_WithInvalidMagicOrZeroCapacity_ThrowsInvalidDataException()
    {
        Assert.Throws<ArgumentNullException>(() => ReadCheckpoint(null!));

        // 1. Invalid magic header
        using var badMagicStream = new MemoryStream();
        var badMagic = 0xDEADBEEF;
        ArenaBinaryIO.WriteValue(badMagicStream, in badMagic);
        badMagicStream.Position = 0;

        Assert.Throws<InvalidDataException>(() => ReadCheckpoint(badMagicStream));

        // 2. Valid magic ("MNFD" = 0x44464E4D), but maxSimplices = 0
        using var zeroCapStream = new MemoryStream();
        uint validMagic = 0x44464E4D;
        uint ten = 10;
        uint zero = 0;
        ArenaBinaryIO.WriteValue(zeroCapStream, in validMagic);
        ArenaBinaryIO.WriteValue(zeroCapStream, in ten);
        ArenaBinaryIO.WriteValue(zeroCapStream, in zero);
        ArenaBinaryIO.WriteValue(zeroCapStream, in ten);
        zeroCapStream.Position = 0;

        Assert.Throws<InvalidDataException>(() => ReadCheckpoint(zeroCapStream));
    }

    [Fact]
    public void ReadCheckpoint_WhenTypeStoreCountsDisagreeWithSimplexCount_ThrowsInvalidDataException()
    {
        var source = Create(maxVertices: 10, maxSimplices: 10, maxAdjacencyBlocks: 10);
        try
        {
            var raw = (ManifoldOverlay*)source;

            // Desynchronize TypeStore31 (Count = 1) from Memory.SimplexCount (Count = 0)
            raw->TypeStore31.Add(new SimplexId(0));

            using var stream = new MemoryStream();
            source->WriteCheckpoint(stream);

            stream.Position = 0;
            Assert.Throws<InvalidDataException>(() => ReadCheckpoint(stream));
        }
        finally
        {
            Destroy(ref source);
        }
    }
}