using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RandomNumberGeneration;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1_Tests.Engine.State.Mesh.Accessors;

public sealed unsafe class RandomAccessorTests
{
    [Fact]
    public void RandomAccessor_GetRandomBoundedAtFour_RespectsBounds()
    {
        var rng = Pcg32.Create(12345, 54321);
        try
        {
            var randomAccessor = new RandomAccessor(null, rng);

            uint maxSeen = 0;
            var minSeen = uint.MaxValue;

            for (var i = 0; i < 100_000; i++)
            {
                var result = randomAccessor.GetRandomBoundedAtFour();

                if (result > maxSeen) maxSeen = result;
                if (result < minSeen) minSeen = result;

                Assert.True(result <= 3, $"Expected <= 3, got {result}");
            }

            Assert.Equal(0u, minSeen);
            Assert.Equal(3u, maxSeen);
        }
        finally
        {
            Pcg32.Destroy(rng);
        }
    }

    [Fact]
    public void RandomAccessor_SelectRandomTypeAndEntities_DrawsFromCorrectStores()
    {
        var m = Create(maxVertices: 10, maxSimplices: 50, maxAdjacencyBlocks: 10);
        var rng = Pcg32.Create(42, 42);

        try
        {
            var raw = (ManifoldOverlay*)m;
            var mutator = new MutatingAccessor(m);
            var randomAccessor = new RandomAccessor(m, rng);

            // Allocate one active vertex via MutatingAccessor
            var expectedVertex = mutator.AddVertex(new TimeSlice(0));

            // Manually inject IDs into the active Type Stores using the Overlay
            raw->TypeStore31.Add(new(10));
            raw->TypeStore31.Add(new(20));
            raw->TypeStore22.Add(new(99));
            raw->TypeStore13.Add(new(33));

            for (var i = 0; i < 10; i++)
            {
                Assert.Equal(expectedVertex, randomAccessor.SelectRandomVertex());
                Assert.Equal(99, randomAccessor.SelectRandomType22().Value);
                Assert.Equal(33, randomAccessor.SelectRandomType13().Value);

                var selected31 = randomAccessor.SelectRandomType31().Value;
                Assert.True(selected31 is 10 or 20);
            }
        }
        finally
        {
            Pcg32.Destroy(rng);
            Destroy(ref m);
        }
    }
}