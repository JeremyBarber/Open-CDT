using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

using RealMove44 = CDT_2plus1.Engine.Topology.Pachner.Move44.Move44;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move44;

public sealed unsafe class Move44PerformTests : IDisposable
{
    private Manifold* _activeManifold;

    public void Dispose()
    {
        if (_activeManifold != null)
        {
            Move44TestArena.FreeArena(_activeManifold);
            _activeManifold = null;
        }
    }

    [Fact]
    public void PerformMove_GivenValidContext_FlipsEdgeAndPreservesCounts()
    {
        _activeManifold = Move44TestArena.CreateValid44Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        SimplexId targetSimplex = new(0);
        ref readonly var sC31 = ref inspector.GetTetrahedron(targetSimplex);
        var targetEdge = TetrahedronValidation.FindEdgeSlot(in sC31, 0, 1);

        RealMove44.IsValidSite(inspector, targetSimplex, targetEdge, out var context);

        var initialVertices = inspector.Adjacency.TotalVertices;
        var initial31s = inspector.N31;
        var initial13s = inspector.N13;
        var initial22s = inspector.N22;

        RealMove44.PerformMove(mutator, in context);

        Assert.Equal(initialVertices, inspector.Adjacency.TotalVertices);
        Assert.Equal(initial31s, inspector.N31);
        Assert.Equal(initial13s, inspector.N13);
        Assert.Equal(initial22s, inspector.N22);

        Assert.False(inspector.Adjacency.EdgeExists(context.VA, context.VB), "Original edge vA-vB was not torn down.");
        Assert.True(inspector.Adjacency.EdgeExists(context.C, context.D), "Alternate diagonal edge C-D was not created.");
    }

    [Fact]
    public void Move44_IsStrictlyReversible_AlgebraicIdentity()
    {
        _activeManifold = Move44TestArena.CreateValid44Cluster();
        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        var initialN0 = inspector.Adjacency.TotalVertices;
        var initial31 = inspector.N31;
        var initial13 = inspector.N13;

        var originalGeometries = new List<int[]>();
        var allActiveIds = inspector.ActiveIds31.ToArray().Concat(inspector.ActiveIds13.ToArray()).ToList();

        foreach (var id in allActiveIds)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            int[] verts = [s.Vertices[0], s.Vertices[1], s.Vertices[2], s.Vertices[3]];
            Array.Sort(verts);
            originalGeometries.Add(verts);
        }

        SimplexId initialTargetSimplex = new(0);
        ref readonly var initialS = ref inspector.GetTetrahedron(initialTargetSimplex);
        var initialTargetEdge = TetrahedronValidation.FindEdgeSlot(in initialS, 0, 1);

        // 1. Perform forward 4 -> 4 (flips vA-vB to C-D)
        RealMove44.IsValidSite(inspector, initialTargetSimplex, initialTargetEdge, out var ctxForward);
        RealMove44.PerformMove(mutator, in ctxForward);

        var vC = ctxForward.C;
        var vD = ctxForward.D;

        // 2. Dynamically find new target and perform reverse 4 -> 4
        var reverseTargetSimplex = SimplexId.Invalid;
        LocalEdgeSlot reverseTargetEdge = default;

        foreach (var id in inspector.ActiveIds31)
        {
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
            if (TetrahedronValidation.Contains(in s, vC.Value) && TetrahedronValidation.Contains(in s, vD.Value))
            {
                reverseTargetSimplex = new SimplexId(id);
                reverseTargetEdge = TetrahedronValidation.FindEdgeSlot(in s, vC.Value, vD.Value);
                break;
            }
        }

        Assert.NotEqual(SimplexId.Invalid, reverseTargetSimplex);

        var isValidReverse = RealMove44.IsValidSite(inspector, reverseTargetSimplex, reverseTargetEdge, out var ctxReverse);
        Assert.True(isValidReverse, "Reverse Move 44 failed validation on the output of the forward move.");

        RealMove44.PerformMove(mutator, in ctxReverse);

        // 3. Assert global counts and exact algebraic identity
        Assert.Equal(initialN0, inspector.Adjacency.TotalVertices);
        Assert.Equal(initial31, inspector.N31);
        Assert.Equal(initial13, inspector.N13);

        var finalActiveIds = inspector.ActiveIds31.ToArray().Concat(inspector.ActiveIds13.ToArray()).ToList();
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

        Assert.Equal(allActiveIds.Count, matchedGeometries);
    }

    [Fact]
    public void ErgodicCycle_4To4Reversals_10000Iterations_RecyclesIDsPerfectly()
    {
        _activeManifold = Move44TestArena.CreateValid44Cluster();
        var raw = (ManifoldOverlay*)_activeManifold;

        var inspector = new InspectingAccessor(_activeManifold);
        var mutator = new MutatingAccessor(_activeManifold);

        var initialVertices = inspector.Adjacency.TotalVertices;
        var initial31 = inspector.N31;
        var initial13 = inspector.N13;
        var initialMemorySimplexCount = raw->Memory.SimplexCount;

        VertexId currentA = new(0);
        VertexId currentB = new(1);

        for (var i = 0; i < 10_000; i++)
        {
            var anchorSimplex = SimplexId.Invalid;
            LocalEdgeSlot anchorEdge = default;

            foreach (var id in inspector.ActiveIds31)
            {
                ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(id));
                if (TetrahedronValidation.Contains(in s, currentA.Value) &&
                    TetrahedronValidation.Contains(in s, currentB.Value))
                {
                    anchorSimplex = new SimplexId(id);
                    anchorEdge = TetrahedronValidation.FindEdgeSlot(in s, currentA.Value, currentB.Value);
                    break;
                }
            }

            Assert.NotEqual(SimplexId.Invalid, anchorSimplex);

            RealMove44.IsValidSite(inspector, anchorSimplex, anchorEdge, out var ctx);
            RealMove44.PerformMove(mutator, in ctx);

            currentA = ctx.C;
            currentB = ctx.D;
        }

        Assert.Equal(initialVertices, inspector.Adjacency.TotalVertices);
        Assert.Equal(initial31, inspector.N31);
        Assert.Equal(initial13, inspector.N13);
        Assert.Equal(initialMemorySimplexCount, raw->Memory.SimplexCount);
    }
}