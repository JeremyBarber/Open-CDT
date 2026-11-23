using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.Topology;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

using RealMove32 = CDT_2plus1.Engine.Topology.Pachner.Move32.Move32;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move32;

public sealed unsafe class Move32ValidationTests : IDisposable
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
    public void IsValidSite_GivenPerfect32Cluster_ReturnsTrueAndMapsContext()
    {
        _activeManifold = Move32TestArena.CreateValid32Cluster();
        var inspector = new InspectingAccessor(_activeManifold);

        ref readonly var s0 = ref inspector.GetTetrahedron(new SimplexId(0));
        var targetEdge = TetrahedronValidation.FindEdgeSlot(in s0, 0, 1);

        var isValid = RealMove32.IsValidSite(inspector, new SimplexId(0), targetEdge, out var context);

        Assert.True(isValid, "Validation incorrectly rejected a valid perfect (3,2) setup.");
        Assert.True(context.Id0.Value == 0 || context.Id1.Value == 0 || context.Id2.Value == 0);
        Assert.True(
            (context.VA.Value == 0 && context.VB.Value == 1) || (context.VA.Value == 1 && context.VB.Value == 0),
            "Poles (VA, VB) were incorrectly identified.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void IsValidSite_ExhaustiveEdgePermutations_CorrectlyDecodesAllOrientations(byte edgeSlotVal)
    {
        _activeManifold = Move32TestArena.CreateValid32Cluster();
        var raw = (ManifoldOverlay*)_activeManifold;
        var inspector = new InspectingAccessor(_activeManifold);

        SimplexId targetSimplex = new(0);
        LocalEdgeSlot targetEdgeSlot = new(edgeSlotVal);

        ref var s0 = ref raw->SimplexPool[targetSimplex.Value];
        (var sA, var sB) = Tetrahedron.GetEdgeVertexSlots(targetEdgeSlot);

        int[] origVerts = [s0.Vertices[0], s0.Vertices[1], s0.Vertices[2], s0.Vertices[3]];
        int[] origNeighs = [s0.NeighborSimplices[0], s0.NeighborSimplices[1], s0.NeighborSimplices[2], s0.NeighborSimplices[3]];

        var origSlot0 = Array.IndexOf(origVerts, 0);
        var origSlot1 = Array.IndexOf(origVerts, 1);

        var remainingOrigSlots = new List<int> { 0, 1, 2, 3 };
        remainingOrigSlots.Remove(origSlot0);
        remainingOrigSlots.Remove(origSlot1);

        var remainingTargetSlots = new List<int> { 0, 1, 2, 3 };
        remainingTargetSlots.Remove(sA.Value);
        remainingTargetSlots.Remove(sB.Value);

        s0.Vertices[sA.Value] = 0;
        s0.NeighborSimplices[sA.Value] = origNeighs[origSlot0];
        s0.Vertices[sB.Value] = 1;
        s0.NeighborSimplices[sB.Value] = origNeighs[origSlot1];

        s0.Vertices[remainingTargetSlots[0]] = origVerts[remainingOrigSlots[0]];
        s0.NeighborSimplices[remainingTargetSlots[0]] = origNeighs[remainingOrigSlots[0]];
        s0.Vertices[remainingTargetSlots[1]] = origVerts[remainingOrigSlots[1]];
        s0.NeighborSimplices[remainingTargetSlots[1]] = origNeighs[remainingOrigSlots[1]];

        var isValid = RealMove32.IsValidSite(inspector, targetSimplex, targetEdgeSlot, out var context);

        Assert.True(isValid, $"Branchless topological decoding failed when the target edge was forced into LocalEdgeSlot {edgeSlotVal}.");
        Assert.True(
            (context.VA.Value == 0 && context.VB.Value == 1) || (context.VA.Value == 1 && context.VB.Value == 0),
            "Poles (VA, VB) were incorrectly identified.");
    }

    [Fact]
    public void IsValidSite_AcrossPeriodicTimeBoundary_ReturnsTrue()
    {
        _activeManifold = Move32TestArena.CreateValid32Cluster_PBC();
        var inspector = new InspectingAccessor(_activeManifold);

        ref readonly var s0 = ref inspector.GetTetrahedron(new SimplexId(0));
        var targetEdge = TetrahedronValidation.FindEdgeSlot(in s0, 0, 1);

        var isValid = RealMove32.IsValidSite(inspector, new SimplexId(0), targetEdge, out _);
        Assert.True(isValid, "Rejected a timelike edge spanning the periodic temporal boundary.");
    }

    [Fact]
    public void IsValidSite_GivenSpacelikeEdge_FailsFast()
    {
        _activeManifold = Move32TestArena.CreateValid32Cluster();
        var inspector = new InspectingAccessor(_activeManifold);

        // In CreateValid32Cluster, vertices 0 (vA) and 2 (p) both reside at t = 10 (spacelike edge)
        ref readonly var s0 = ref inspector.GetTetrahedron(new SimplexId(0));
        var spacelikeEdge = TetrahedronValidation.FindEdgeSlot(in s0, 0, 2);

        var isValid = RealMove32.IsValidSite(inspector, new SimplexId(0), spacelikeEdge, out _);
        Assert.False(isValid, "Accepted a purely spacelike edge for a 3->2 move.");
    }

    [Fact]
    public void IsValidSite_GivenEdgeDegreeNotThree_FailsFast()
    {
        _activeManifold = Move32TestArena.CreateInvalid32Cluster_WrongDegree();
        var inspector = new InspectingAccessor(_activeManifold);

        ref readonly var s0 = ref inspector.GetTetrahedron(new SimplexId(0));
        var targetEdge = TetrahedronValidation.FindEdgeSlot(in s0, 0, 1);

        Assert.False(RealMove32.IsValidSite(inspector, new SimplexId(0), targetEdge, out _));
    }

    [Fact]
    public void IsValidSite_GivenWrongSimplexTypes_FailsFast()
    {
        _activeManifold = Move32TestArena.CreateInvalid32Cluster_WrongTypes();
        var inspector = new InspectingAccessor(_activeManifold);

        ref readonly var s0 = ref inspector.GetTetrahedron(new SimplexId(0));
        var targetEdge = TetrahedronValidation.FindEdgeSlot(in s0, 0, 1);

        Assert.False(RealMove32.IsValidSite(inspector, new SimplexId(0), targetEdge, out _));
    }

    [Fact]
    public void IsValidSite_GivenEquatorialTriangleAlreadyExists_FailsDegeneracyCheck()
    {
        _activeManifold = Move32TestArena.CreateInvalid32Cluster_DegenerateTriangle();
        var inspector = new InspectingAccessor(_activeManifold);

        ref readonly var s0 = ref inspector.GetTetrahedron(new SimplexId(0));
        var targetEdge = TetrahedronValidation.FindEdgeSlot(in s0, 0, 1);

        Assert.False(RealMove32.IsValidSite(inspector, new SimplexId(0), targetEdge, out _));
    }
}