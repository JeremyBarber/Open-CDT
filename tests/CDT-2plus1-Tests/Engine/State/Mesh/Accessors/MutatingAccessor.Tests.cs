using CDT_2plus1.Engine.Topology;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_2plus1_Tests.Engine.State.Mesh.Accessors;

public sealed unsafe class MutatingAccessorTests
{
    [Fact]
    public void Mutator_LinkNeighbors_MaintainsReciprocity()
    {
        var m = Create(maxVertices: 10, maxSimplices: 10, maxAdjacencyBlocks: 10);
        try
        {
            var raw = (ManifoldOverlay*)m;
            var mutator = new MutatingAccessor(m);

            var sA = new SimplexId(2);
            var sB = new SimplexId(5);
            var facetA = new LocalFacetSlot(1);
            var facetB = new LocalFacetSlot(3);

            mutator.LinkNeighbors(sA, facetA, sB, facetB);

            Assert.Equal(sB.Value, raw->SimplexPool[sA.Value].NeighborSimplices[facetA.Value]);
            Assert.Equal(sA.Value, raw->SimplexPool[sB.Value].NeighborSimplices[facetB.Value]);
        }
        finally
        {
            Destroy(ref m);
        }
    }

    [Fact]
    public void Mutator_SimplexLifecycle_MaintainsFreeListAndTypeStore()
    {
        var m = Create(maxVertices: 10, maxSimplices: 10, maxAdjacencyBlocks: 10);
        try
        {
            var raw = (ManifoldOverlay*)m;
            var mutator = new MutatingAccessor(m);

            // 1. Register a (2,2) simplex
            var id = mutator.RegisterSimplex(
                new VertexId(0), new VertexId(1), new VertexId(2), new VertexId(3),
                SimplexType.TwoTwo);

            Assert.Equal(1, raw->TypeStore22.Count);
            Assert.Equal(1, mutator.Inspect.Adjacency.TotalSimplices);
            Assert.Equal((byte)SimplexType.TwoTwo, raw->SimplexTypes[id.Value]);

            // 2. Unregister it
            mutator.UnregisterSimplex(id);

            Assert.Equal(0, raw->TypeStore22.Count);
            Assert.Equal(0, mutator.Inspect.Adjacency.TotalSimplices);
            Assert.Equal(InvalidDataByte, raw->SimplexTypes[id.Value]);

            // 3. Register a new simplex — the freed ID must be recycled from the LIFO stack
            var nextId = mutator.RegisterSimplex(
                new VertexId(4), new VertexId(5), new VertexId(6), new VertexId(7),
                SimplexType.ThreeOne);

            Assert.Equal(id.Value, nextId.Value);
            Assert.Equal(1, raw->TypeStore31.Count);
        }
        finally
        {
            Destroy(ref m);
        }
    }

    [Fact]
    public void Mutator_VertexLifecycle_AssignsAndClearsTimeslices()
    {
        var m = Create(maxVertices: 10, maxSimplices: 10, maxAdjacencyBlocks: 10);
        try
        {
            var raw = (ManifoldOverlay*)m;
            var mutator = new MutatingAccessor(m);

            var vId = mutator.AddVertex(new TimeSlice(5));

            Assert.Equal(1, mutator.Inspect.Adjacency.TotalVertices);
            Assert.Equal(5, raw->VertexTimes[vId.Value]);

            mutator.RemoveVertex(vId);

            Assert.Equal(0, mutator.Inspect.Adjacency.TotalVertices);
            Assert.Equal(InvalidData, raw->VertexTimes[vId.Value]);
        }
        finally
        {
            Destroy(ref m);
        }
    }

    [Fact]
    public void Mutator_VertexAdjacency_ProperlyLinksAndUnlinks()
    {
        var m = Create(maxVertices: 10, maxSimplices: 10, maxAdjacencyBlocks: 10);
        try
        {
            var mutator = new MutatingAccessor(m);
            var inspector = mutator.Inspect;

            var vTarget = new VertexId(5);
            var s1 = new SimplexId(1);
            var s2 = new SimplexId(2);

            mutator.AddSimplexToVertex(vTarget, s1);
            mutator.AddSimplexToVertex(vTarget, s2);

            var buffer = stackalloc SimplexId[2];
            var success = inspector.Adjacency.GetExactlyNSimplices(vTarget, buffer, expected: 2);

            Assert.True(success, "Inspector failed to retrieve the expected number of simplices.");
            Assert.True((buffer[0].Value == 1 && buffer[1].Value == 2) ||
                        (buffer[0].Value == 2 && buffer[1].Value == 1));

            mutator.RemoveSimplexFromVertex(vTarget, s1);

            var singleBuffer = stackalloc SimplexId[1];
            var singleSuccess = inspector.Adjacency.GetExactlyNSimplices(vTarget, singleBuffer, expected: 1);

            Assert.True(singleSuccess);
            Assert.Equal(2, singleBuffer[0].Value);
        }
        finally
        {
            Destroy(ref m);
        }
    }
}