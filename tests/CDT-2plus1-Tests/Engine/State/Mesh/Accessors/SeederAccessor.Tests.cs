using CDT_2plus1.Engine.Topology;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1_Tests.Engine.State.Mesh.Accessors;

public sealed unsafe class SeederAccessorTests
{
    [Fact]
    public void Seeder_InvertOrientation_SwapsVerticesAndNeighborsCorrectly()
    {
        var m = Create(maxVertices: 10, maxSimplices: 10, maxAdjacencyBlocks: 10);
        try
        {
            var raw = (ManifoldOverlay*)m;
            var seeder = new SeederAccessor(m);

            raw->SimplexPool[5].Vertices[0] = 100;
            raw->SimplexPool[5].Vertices[1] = 101;
            raw->SimplexPool[5].Vertices[2] = 102;
            raw->SimplexPool[5].Vertices[3] = 103;

            raw->SimplexPool[5].NeighborSimplices[0] = 200;
            raw->SimplexPool[5].NeighborSimplices[1] = 201;
            raw->SimplexPool[5].NeighborSimplices[2] = 202;
            raw->SimplexPool[5].NeighborSimplices[3] = 203;

            seeder.InvertOrientation(new SimplexId(5));

            // Slots 0 and 1 must be swapped in both Vertices and NeighborSimplices
            Assert.Equal(101, raw->SimplexPool[5].Vertices[0]);
            Assert.Equal(100, raw->SimplexPool[5].Vertices[1]);
            Assert.Equal(201, raw->SimplexPool[5].NeighborSimplices[0]);
            Assert.Equal(200, raw->SimplexPool[5].NeighborSimplices[1]);

            // Slots 2 and 3 must remain untouched
            Assert.Equal(102, raw->SimplexPool[5].Vertices[2]);
            Assert.Equal(103, raw->SimplexPool[5].Vertices[3]);
            Assert.Equal(202, raw->SimplexPool[5].NeighborSimplices[2]);
            Assert.Equal(203, raw->SimplexPool[5].NeighborSimplices[3]);
        }
        finally
        {
            Destroy(ref m);
        }
    }

    [Fact]
    public void Seeder_PassThroughMethods_WorkAsExpected()
    {
        var m = Create(maxVertices: 10, maxSimplices: 10, maxAdjacencyBlocks: 10);
        try
        {
            var seeder = new SeederAccessor(m);

            var v0 = seeder.SeedVertex(new TimeSlice(0));
            var v1 = seeder.SeedVertex(new TimeSlice(0));
            var v2 = seeder.SeedVertex(new TimeSlice(0));
            var v3 = seeder.SeedVertex(new TimeSlice(1));

            var sId = seeder.SeedSimplex(v0, v1, v2, v3, SimplexType.ThreeOne);

            Assert.True(seeder.Mutate.Inspect.SimplexIsType(sId, SimplexType.ThreeOne));
            Assert.Equal(1, seeder.Mutate.Inspect.Adjacency.TotalSimplices);
            Assert.Equal(v0.Value, seeder.GetSimplex(sId).Vertices[0]);
        }
        finally
        {
            Destroy(ref m);
        }
    }
}