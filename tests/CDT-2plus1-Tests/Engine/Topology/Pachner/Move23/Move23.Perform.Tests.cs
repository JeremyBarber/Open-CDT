using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.Topology;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

using RealMove23 = CDT_2plus1.Engine.Topology.Pachner.Move23.Move23;
using RealMove32 = CDT_2plus1.Engine.Topology.Pachner.Move32.Move32;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move23;

public sealed unsafe class Move23PerformTests : IDisposable
{
    private Manifold* _activeManifold;

    public void Dispose()
    {
        if (_activeManifold != null)
        {
            Move23TestArena.FreeArena(_activeManifold);
            _activeManifold = null;
        }
    }

    [Fact]
    public void PerformMove_GivenValidContext_WiresNewSimplicesCorrectly()
    {
        _activeManifold = Move23TestArena.CreateValid23Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        RealMove23.IsValidSite(inspector, new SimplexId(0), new LocalFacetSlot(3), out var context);
        var vTarget = context.VTarget.Value;
        var vNeighbour = context.VNeighbour.Value;

        RealMove23.PerformMove(mutator, in context);

        Assert.True(inspector.Adjacency.EdgeExists(context.VTarget, context.VNeighbour), "Central axis was not created.");

        var newAxisCount = 0;
        var newIds = new int[3];

        void CheckAxis(ReadOnlySpan<int> activeIds)
        {
            foreach (var id in activeIds)
            {
                ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
                var hasTarget = s.Vertices[0] == vTarget || s.Vertices[1] == vTarget || s.Vertices[2] == vTarget || s.Vertices[3] == vTarget;
                var hasNeighbour = s.Vertices[0] == vNeighbour || s.Vertices[1] == vNeighbour || s.Vertices[2] == vNeighbour || s.Vertices[3] == vNeighbour;

                if (hasTarget && hasNeighbour)
                {
                    if (newAxisCount < 3)
                        newIds[newAxisCount] = id;
                    newAxisCount++;
                }
            }
        }

        CheckAxis(inspector.ActiveIds22);
        CheckAxis(inspector.ActiveIds31);
        CheckAxis(inspector.ActiveIds13);

        Assert.Equal(3, newAxisCount);

        for (var i = 0; i < 3; i++)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(newIds[i]));

            var neighbor2 = s.NeighborSimplices[2];
            var neighbor3 = s.NeighborSimplices[3];

            Assert.Contains(neighbor2, newIds);
            Assert.Contains(neighbor3, newIds);
            Assert.NotEqual(newIds[i], neighbor2);
            Assert.NotEqual(newIds[i], neighbor3);
        }
    }

    [Fact]
    public void PerformMove_EnforcesStrictDeltaInvariant()
    {
        _activeManifold = Move23TestArena.CreateValid23Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        var initialN3 = inspector.Adjacency.TotalSimplices;
        var initialN0 = inspector.Adjacency.TotalVertices;
        var initialN31 = inspector.N31;
        var initialN22 = inspector.N22;
        var initialN13 = inspector.N13;

        RealMove23.IsValidSite(inspector, new SimplexId(0), new LocalFacetSlot(3), out var context);
        RealMove23.PerformMove(mutator, in context);

        Assert.Equal(initialN3 + 1, inspector.Adjacency.TotalSimplices);
        Assert.Equal(initialN0, inspector.Adjacency.TotalVertices);
        Assert.Equal(initialN31, inspector.N31);
        Assert.Equal(initialN22 + 1, inspector.N22);
        Assert.Equal(initialN13, inspector.N13);
    }

    [Fact]
    public void PerformMove_AcrossPeriodicTimeBoundary_PreservesAsymmetricSimplexType()
    {
        _activeManifold = Move23TestArena.CreateValid23Cluster_PBC();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        Assert.Equal(1, inspector.N31);
        Assert.Equal(1, inspector.N22);
        Assert.Equal(0, inspector.N13);

        var isValid = RealMove23.IsValidSite(inspector, new SimplexId(0), new LocalFacetSlot(3), out var context);
        Assert.True(isValid);

        RealMove23.PerformMove(mutator, in context);

        // Must preserve N31 = 1 and N13 = 0 across the t = 3 -> t = 0 periodic boundary
        Assert.Equal(1, inspector.N31);
        Assert.Equal(2, inspector.N22);
        Assert.Equal(0, inspector.N13);
    }

    [Fact]
    public void Move23_FollowedByMove32_ReturnsManifoldToIdentity()
    {
        _activeManifold = Move23TestArena.CreateValid23Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        var initialN0 = inspector.Adjacency.TotalVertices;
        var initialN3 = inspector.Adjacency.TotalSimplices;

        ref readonly var origS0 = ref inspector.GetTetrahedron(new SimplexId(0));
        ref readonly var origS1 = ref inspector.GetTetrahedron(new SimplexId(1));
        int[] s0Verts = [origS0.Vertices[0], origS0.Vertices[1], origS0.Vertices[2], origS0.Vertices[3]];
        int[] s1Verts = [origS1.Vertices[0], origS1.Vertices[1], origS1.Vertices[2], origS1.Vertices[3]];
        Array.Sort(s0Verts);
        Array.Sort(s1Verts);

        RealMove23.IsValidSite(inspector, new SimplexId(0), new LocalFacetSlot(3), out var ctx23);
        RealMove23.PerformMove(mutator, in ctx23);

        var target32 = SimplexId.Invalid;
        LocalEdgeSlot edge32 = default;

        var allActiveIds = inspector.ActiveIds22.ToArray()
            .Concat(inspector.ActiveIds31.ToArray())
            .Concat(inspector.ActiveIds13.ToArray());

        foreach (var id in allActiveIds)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            for (byte e = 0; e < 6; e++)
            {
                (var sA, var sB) = Tetrahedron.GetEdgeVertexSlots(new LocalEdgeSlot(e));
                var vA = s.Vertices[sA.Value];
                var vB = s.Vertices[sB.Value];
                if ((vA == ctx23.VTarget.Value && vB == ctx23.VNeighbour.Value) ||
                    (vA == ctx23.VNeighbour.Value && vB == ctx23.VTarget.Value))
                {
                    target32 = new SimplexId(id);
                    edge32 = new LocalEdgeSlot(e);
                    break;
                }
            }
            if (target32 != SimplexId.Invalid)
                break;
        }

        Assert.NotEqual(SimplexId.Invalid, target32);

        var isValid32 = RealMove32.IsValidSite(inspector, target32, edge32, out var ctx32);
        Assert.True(isValid32, "Move 32 validation rejected the mathematically valid 3->2 site.");

        RealMove32.PerformMove(mutator, in ctx32);

        Assert.Equal(initialN0, inspector.Adjacency.TotalVertices);
        Assert.Equal(initialN3, inspector.Adjacency.TotalSimplices);

        bool foundS0 = false, foundS1 = false;
        var finalActiveIds = inspector.ActiveIds22.ToArray()
            .Concat(inspector.ActiveIds31.ToArray())
            .Concat(inspector.ActiveIds13.ToArray());

        foreach (var id in finalActiveIds)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            int[] currentVerts = [s.Vertices[0], s.Vertices[1], s.Vertices[2], s.Vertices[3]];
            Array.Sort(currentVerts);
            if (currentVerts.SequenceEqual(s0Verts)) foundS0 = true;
            if (currentVerts.SequenceEqual(s1Verts)) foundS1 = true;
        }

        Assert.True(foundS0 && foundS1, "Manifold did not geometrically return to identity state.");
    }
}