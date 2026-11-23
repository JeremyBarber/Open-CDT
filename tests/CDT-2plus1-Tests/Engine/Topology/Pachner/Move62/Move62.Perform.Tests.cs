using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

using RealMove26 = CDT_2plus1.Engine.Topology.Pachner.Move26.Move26;
using RealMove62 = CDT_2plus1.Engine.Topology.Pachner.Move62.Move62;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move62;

public sealed unsafe class Move62PerformTests : IDisposable
{
    private Manifold* _activeManifold;

    public void Dispose()
    {
        if (_activeManifold != null)
        {
            Move26_62TestArena.FreeArena(_activeManifold);
            _activeManifold = null;
        }
    }

    [Fact]
    public void PerformMove_EnforcesStrictDeltaInvariant_AndDeallocatesVertex()
    {
        _activeManifold = Move26_62TestArena.CreateValid62Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        var initialVertices = inspector.Adjacency.TotalVertices;
        var initial31s = inspector.N31;
        var initial13s = inspector.N13;
        var initialN3 = inspector.Adjacency.TotalSimplices;

        RealMove62.IsValidSite(inspector, new VertexId(0), out var context);
        RealMove62.PerformMove(mutator, in context);

        Assert.Equal(initialVertices - 1, inspector.Adjacency.TotalVertices);
        Assert.Equal(initialN3 - 4, inspector.Adjacency.TotalSimplices);
        Assert.Equal(initial31s - 2, inspector.N31);
        Assert.Equal(initial13s - 2, inspector.N13);
    }

    [Fact]
    public void PerformMove_GivenValidContext_WiresNewSimplicesAcrossEquator()
    {
        _activeManifold = Move26_62TestArena.CreateValid62Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        RealMove62.IsValidSite(inspector, new VertexId(0), out var context);
        RealMove62.PerformMove(mutator, in context);

        var new31 = SimplexId.Invalid;
        var new13 = SimplexId.Invalid;

        int vA = context.A.Value, vB = context.B.Value, vC = context.C.Value;

        foreach (var id in inspector.ActiveIds31)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            if (TetrahedronValidation.Contains(in s, vA) &&
                TetrahedronValidation.Contains(in s, vB) &&
                TetrahedronValidation.Contains(in s, vC))
            {
                new31 = new SimplexId(id);
                break;
            }
        }

        foreach (var id in inspector.ActiveIds13)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            if (TetrahedronValidation.Contains(in s, vA) &&
                TetrahedronValidation.Contains(in s, vB) &&
                TetrahedronValidation.Contains(in s, vC))
            {
                new13 = new SimplexId(id);
                break;
            }
        }

        Assert.NotEqual(SimplexId.Invalid, new31);
        Assert.NotEqual(SimplexId.Invalid, new13);

        ref readonly var s31 = ref inspector.GetTetrahedron(new31);
        ref readonly var s13 = ref inspector.GetTetrahedron(new13);

        var s31PointsTo13 = false;
        var s13PointsTo31 = false;

        for (var i = 0; i < 4; i++)
        {
            if (s31.NeighborSimplices[i] == new13.Value) s31PointsTo13 = true;
            if (s13.NeighborSimplices[i] == new31.Value) s13PointsTo31 = true;
        }

        Assert.True(s31PointsTo13, "The new (3,1) did not wire correctly to the (1,3).");
        Assert.True(s13PointsTo31, "The new (1,3) did not wire correctly to the (3,1).");
    }

    [Fact]
    public void Move62_FollowedByMove26_ReturnsManifoldToIdentity()
    {
        _activeManifold = Move26_62TestArena.CreateValid62Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        var initialN0 = inspector.Adjacency.TotalVertices;
        var initialN3 = inspector.Adjacency.TotalSimplices;
        var initial31 = inspector.N31;
        var initial13 = inspector.N13;

        var originalGeometries = new List<int[]>();
        var allActiveIds = inspector.ActiveIds31.ToArray().Concat(inspector.ActiveIds13.ToArray());

        foreach (var id in allActiveIds)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            int[] verts = [s.Vertices[0], s.Vertices[1], s.Vertices[2], s.Vertices[3]];
            Array.Sort(verts);
            originalGeometries.Add(verts);
        }

        RealMove62.IsValidSite(inspector, new VertexId(0), out var initialCtx);
        RealMove62.PerformMove(mutator, in initialCtx);

        var target13 = SimplexId.Invalid;
        foreach (var id in inspector.ActiveIds13)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));

            if (TetrahedronValidation.Contains(in s, initialCtx.A.Value) &&
                TetrahedronValidation.Contains(in s, initialCtx.B.Value) &&
                TetrahedronValidation.Contains(in s, initialCtx.C.Value) &&
                TetrahedronValidation.Contains(in s, initialCtx.V.Value))
            {
                target13 = new SimplexId(id);
                break;
            }
        }

        Assert.NotEqual(SimplexId.Invalid, target13);

        var isValid26 = RealMove26.IsValidSite(inspector, target13, out var ctx26);
        Assert.True(isValid26, "Move 26 Reversal failed validation on the output of a 6->2 move.");
        RealMove26.PerformMove(mutator, in ctx26);

        Assert.Equal(initialN0, inspector.Adjacency.TotalVertices);
        Assert.Equal(initialN3, inspector.Adjacency.TotalSimplices);
        Assert.Equal(initial31, inspector.N31);
        Assert.Equal(initial13, inspector.N13);

        var finalActiveIds = inspector.ActiveIds31.ToArray().Concat(inspector.ActiveIds13.ToArray());
        var matchedGeometries = 0;

        foreach (var id in finalActiveIds)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            int[] currentVerts = [s.Vertices[0], s.Vertices[1], s.Vertices[2], s.Vertices[3]];
            Array.Sort(currentVerts);

            for (var i = 0; i < originalGeometries.Count; i++)
            {
                if (originalGeometries[i].SequenceEqual(currentVerts))
                {
                    matchedGeometries++;
                    originalGeometries.RemoveAt(i);
                    break;
                }
            }
        }

        Assert.Equal(6, matchedGeometries);
    }
}