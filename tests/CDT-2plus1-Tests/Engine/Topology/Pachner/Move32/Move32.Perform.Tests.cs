using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

using RealMove23 = CDT_2plus1.Engine.Topology.Pachner.Move23.Move23;
using RealMove32 = CDT_2plus1.Engine.Topology.Pachner.Move32.Move32;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move32;

public sealed unsafe class Move32PerformTests : IDisposable
{
    private Manifold* _activeManifold;

    public void Dispose()
    {
        if (_activeManifold != null)
        {
            Move32TestArena.FreeArena(_activeManifold);
            _activeManifold = null;
        }
    }

    [Fact]
    public void PerformMove_GivenValidContext_WiresNewSimplicesCorrectly()
    {
        _activeManifold = Move32TestArena.CreateValid32Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        ref readonly var s0 = ref inspector.GetTetrahedron(new SimplexId(0));
        var targetEdge = TetrahedronValidation.FindEdgeSlot(in s0, 0, 1);

        RealMove32.IsValidSite(inspector, new SimplexId(0), targetEdge, out var context);
        RealMove32.PerformMove(mutator, in context);

        // 1. Assert the old axis was destroyed
        Assert.False(inspector.Adjacency.EdgeExists(context.VA, context.VB), "Central timelike edge was not destroyed.");

        // 2. Assert the new bounding face was registered
        Assert.True(inspector.Adjacency.TriangleExists(context.P, context.Q, context.R), "Bounding face was not created.");

        // 3. Find the 2 newly generated simplices using the active IDs
        var newCount = 0;
        var newIds = new int[2];

        var allActiveIds = inspector.ActiveIds22.ToArray()
            .Concat(inspector.ActiveIds31.ToArray())
            .Concat(inspector.ActiveIds13.ToArray());

        foreach (var id in allActiveIds)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            var hasEquator =
                TetrahedronValidation.Contains(in s, context.P.Value) &&
                TetrahedronValidation.Contains(in s, context.Q.Value) &&
                TetrahedronValidation.Contains(in s, context.R.Value);

            if (hasEquator)
            {
                if (newCount < 2)
                    newIds[newCount] = id;
                newCount++;
            }
        }

        Assert.Equal(2, newCount);

        // 4. Assert internal wiring: The two new simplices must point to each other
        ref readonly var newS0 = ref inspector.GetTetrahedron(new SimplexId(newIds[0]));
        ref readonly var newS1 = ref inspector.GetTetrahedron(new SimplexId(newIds[1]));

        var s0PointsToS1 = false;
        var s1PointsToS0 = false;

        for (byte f = 0; f < 4; f++)
        {
            if (newS0.NeighborSimplices[f] == newIds[1]) s0PointsToS1 = true;
            if (newS1.NeighborSimplices[f] == newIds[0]) s1PointsToS0 = true;
        }

        Assert.True(s0PointsToS1, "The first new simplex did not correctly register the second as a neighbor.");
        Assert.True(s1PointsToS0, "The second new simplex did not correctly register the first as a neighbor.");
    }

    [Fact]
    public void PerformMove_EnforcesStrictDeltaInvariant()
    {
        _activeManifold = Move32TestArena.CreateValid32Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        var initialN3 = inspector.Adjacency.TotalSimplices;
        var initialN0 = inspector.Adjacency.TotalVertices;

        ref readonly var s0 = ref inspector.GetTetrahedron(new SimplexId(0));
        RealMove32.IsValidSite(inspector, new SimplexId(0), TetrahedronValidation.FindEdgeSlot(in s0, 0, 1), out var context);

        RealMove32.PerformMove(mutator, in context);

        Assert.Equal(initialN3 - 1, inspector.Adjacency.TotalSimplices);
        Assert.Equal(initialN0, inspector.Adjacency.TotalVertices);
    }

    [Fact]
    public void Move32_FollowedByMove23_ReturnsManifoldToIdentity()
    {
        _activeManifold = Move32TestArena.CreateValid32Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        var initialN0 = inspector.Adjacency.TotalVertices;
        var initialN3 = inspector.Adjacency.TotalSimplices;

        ref readonly var origS0 = ref inspector.GetTetrahedron(new SimplexId(0));
        ref readonly var origS1 = ref inspector.GetTetrahedron(new SimplexId(1));
        ref readonly var origS2 = ref inspector.GetTetrahedron(new SimplexId(2));

        int[] s0Verts = [origS0.Vertices[0], origS0.Vertices[1], origS0.Vertices[2], origS0.Vertices[3]];
        int[] s1Verts = [origS1.Vertices[0], origS1.Vertices[1], origS1.Vertices[2], origS1.Vertices[3]];
        int[] s2Verts = [origS2.Vertices[0], origS2.Vertices[1], origS2.Vertices[2], origS2.Vertices[3]];
        Array.Sort(s0Verts);
        Array.Sort(s1Verts);
        Array.Sort(s2Verts);

        ref readonly var startS0 = ref inspector.GetTetrahedron(new SimplexId(0));
        RealMove32.IsValidSite(inspector, new SimplexId(0), TetrahedronValidation.FindEdgeSlot(in startS0, 0, 1), out var ctx32);
        RealMove32.PerformMove(mutator, in ctx32);

        var target23 = SimplexId.Invalid;
        LocalFacetSlot face23 = default;

        // Move23 requires starting from the (2,2) simplex
        foreach (var id in inspector.ActiveIds22)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));

            if (TetrahedronValidation.Contains(in s, ctx32.P.Value) &&
                TetrahedronValidation.Contains(in s, ctx32.Q.Value) &&
                TetrahedronValidation.Contains(in s, ctx32.R.Value))
            {
                target23 = new SimplexId(id);
                for (byte f = 0; f < 4; f++)
                {
                    if (s.Vertices[f] != ctx32.P.Value &&
                        s.Vertices[f] != ctx32.Q.Value &&
                        s.Vertices[f] != ctx32.R.Value)
                    {
                        face23 = new LocalFacetSlot(f);
                        break;
                    }
                }
                break;
            }
        }

        Assert.True(RealMove23.IsValidSite(inspector, target23, face23, out var ctx23), "Move 23 Reversal failed validation.");
        RealMove23.PerformMove(mutator, in ctx23);

        Assert.Equal(initialN0, inspector.Adjacency.TotalVertices);
        Assert.Equal(initialN3, inspector.Adjacency.TotalSimplices);

        bool foundS0 = false, foundS1 = false, foundS2 = false;
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
            if (currentVerts.SequenceEqual(s2Verts)) foundS2 = true;
        }

        Assert.True(foundS0 && foundS1 && foundS2, "Manifold did not geometrically return to identity state.");
    }
}