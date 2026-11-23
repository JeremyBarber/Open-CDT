using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

using RealMove26 = CDT_2plus1.Engine.Topology.Pachner.Move26.Move26;
using RealMove62 = CDT_2plus1.Engine.Topology.Pachner.Move62.Move62;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move26;

public sealed unsafe class Move26PerformTests : IDisposable
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
    public void PerformMove_EnforcesStrictDeltaInvariant_AndAllocatesVertex()
    {
        _activeManifold = Move26_62TestArena.CreateValid26Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        var initialN0 = inspector.Adjacency.TotalVertices;
        var initialN3 = inspector.Adjacency.TotalSimplices;
        var initialN31 = inspector.N31;
        var initialN13 = inspector.N13;

        RealMove26.IsValidSite(inspector, new SimplexId(1), out var context);
        RealMove26.PerformMove(mutator, in context);

        Assert.Equal(initialN0 + 1, inspector.Adjacency.TotalVertices);
        Assert.Equal(initialN3 + 4, inspector.Adjacency.TotalSimplices);
        Assert.Equal(initialN31 + 2, inspector.N31);
        Assert.Equal(initialN13 + 2, inspector.N13);
    }

    [Fact]
    public void PerformMove_GivenValidContext_WiresNewSimplicesAroundCenter()
    {
        _activeManifold = Move26_62TestArena.CreateValid26Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        RealMove26.IsValidSite(inspector, new SimplexId(1), out var context);
        RealMove26.PerformMove(mutator, in context);

        VertexId newVertex = new(5);

        var new31Count = 0;
        var new13Count = 0;

        foreach (var id in inspector.ActiveIds31)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            if (TetrahedronValidation.Contains(in s, newVertex.Value))
                new31Count++;
        }

        foreach (var id in inspector.ActiveIds13)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            if (TetrahedronValidation.Contains(in s, newVertex.Value))
                new13Count++;
        }

        Assert.Equal(3, new31Count);
        Assert.Equal(3, new13Count);
    }

    [Fact]
    public void Move26_FollowedByMove62_ReturnsManifoldToIdentity()
    {
        _activeManifold = Move26_62TestArena.CreateValid26Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        var initialN0 = inspector.Adjacency.TotalVertices;
        var initialN3 = inspector.Adjacency.TotalSimplices;
        var initialN31 = inspector.N31;
        var initialN13 = inspector.N13;

        var originalGeometries = new List<int[]>();
        var allActiveIds = inspector.ActiveIds31.ToArray().Concat(inspector.ActiveIds13.ToArray());

        foreach (var id in allActiveIds)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            int[] verts = [s.Vertices[0], s.Vertices[1], s.Vertices[2], s.Vertices[3]];
            Array.Sort(verts);
            originalGeometries.Add(verts);
        }

        var originalVertices = new HashSet<int>(originalGeometries.SelectMany(g => g));

        RealMove26.IsValidSite(inspector, new SimplexId(1), out var ctx26);
        RealMove26.PerformMove(mutator, in ctx26);

        var centerVertexVal = -1;
        foreach (var id in inspector.ActiveIds31)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            for (var i = 0; i < 4; i++)
            {
                if (!originalVertices.Contains(s.Vertices[i]))
                {
                    centerVertexVal = s.Vertices[i];
                    break;
                }
            }
            if (centerVertexVal != -1)
                break;
        }
        Assert.NotEqual(-1, centerVertexVal);

        var isValid62 = RealMove62.IsValidSite(inspector, new VertexId(centerVertexVal), out var ctx62);
        Assert.True(isValid62, "Move 62 Reversal failed validation.");
        RealMove62.PerformMove(mutator, in ctx62);

        Assert.Equal(initialN0, inspector.Adjacency.TotalVertices);
        Assert.Equal(initialN3, inspector.Adjacency.TotalSimplices);
        Assert.Equal(initialN31, inspector.N31);
        Assert.Equal(initialN13, inspector.N13);

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

        Assert.Equal(2, matchedGeometries);
    }
}