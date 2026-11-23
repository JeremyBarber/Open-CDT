using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.Topology;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

using RealMove44 = CDT_2plus1.Engine.Topology.Pachner.Move44.Move44;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move44;

public sealed unsafe class Move44ValidationTests : IDisposable
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
    public void IsValidSite_GivenPerfect44Cluster_ReturnsTrueAndMapsContext()
    {
        _activeManifold = Move44TestArena.CreateValid44Cluster();
        var inspector = new InspectingAccessor(_activeManifold);

        SimplexId targetSimplex = new(0);
        ref readonly var sC31 = ref inspector.GetTetrahedron(targetSimplex);
        var targetEdge = TetrahedronValidation.FindEdgeSlot(in sC31, 0, 1);

        var isValid = RealMove44.IsValidSite(inspector, targetSimplex, targetEdge, out var context);

        Assert.True(isValid);
        Assert.Equal(0, context.VA.Value);
        Assert.Equal(1, context.VB.Value);
        Assert.True(
            (context.C.Value == 2 && context.D.Value == 3) ||
            (context.C.Value == 3 && context.D.Value == 2),
            "Failed to correctly identify opposite equatorial vertices C and D.");
    }

    [Fact]
    public void IsValidSite_GivenDegenerateDiagonal_FailsLinkCondition()
    {
        _activeManifold = Move44TestArena.CreateInvalid44Cluster_DegenerateDiagonal();
        var inspector = new InspectingAccessor(_activeManifold);

        SimplexId targetSimplex = new(0);
        ref readonly var sC31 = ref inspector.GetTetrahedron(targetSimplex);
        var targetEdge = TetrahedronValidation.FindEdgeSlot(in sC31, 0, 1);

        var isValid = RealMove44.IsValidSite(inspector, targetSimplex, targetEdge, out _);

        Assert.False(isValid, "Accepted a site where the alternate diagonal edge already exists.");
    }

    [Fact]
    public void IsValidSite_AcrossPeriodicTimeBoundary_ReturnsTrue()
    {
        _activeManifold = Move44TestArena.CreateValid44Cluster_PBC();
        var inspector = new InspectingAccessor(_activeManifold);

        SimplexId targetSimplex = new(0);
        ref readonly var s0 = ref inspector.GetTetrahedron(targetSimplex);
        var targetEdge = TetrahedronValidation.FindEdgeSlot(in s0, 0, 1);

        var isValid = RealMove44.IsValidSite(inspector, targetSimplex, targetEdge, out _);

        Assert.True(isValid, "Move44 rejected a spatial edge flip when the poles spanned the periodic boundary.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void IsValidSite_ExhaustiveEdgeSlotPermutations_CorrectlyIdentifies44(byte targetEdgeSlotVal)
    {
        _activeManifold = Move44TestArena.CreateValid44Cluster();
        var raw = (ManifoldOverlay*)_activeManifold;
        var inspector = new InspectingAccessor(_activeManifold);

        SimplexId targetSimplex = new(0);
        LocalEdgeSlot targetEdgeSlot = new(targetEdgeSlotVal);

        ref var s0 = ref raw->SimplexPool[targetSimplex.Value];
        (var sA, var sB) = Tetrahedron.GetEdgeVertexSlots(targetEdgeSlot);

        int[] newVerts = [s0.Vertices[0], s0.Vertices[1], s0.Vertices[2], s0.Vertices[3]];
        int[] newNeighs = [s0.NeighborSimplices[0], s0.NeighborSimplices[1], s0.NeighborSimplices[2], s0.NeighborSimplices[3]];

        void Swap(int idx1, int idx2)
        {
            if (idx1 == idx2) return;
            (newVerts[idx1], newVerts[idx2]) = (newVerts[idx2], newVerts[idx1]);
            (newNeighs[idx1], newNeighs[idx2]) = (newNeighs[idx2], newNeighs[idx1]);
        }

        Swap(Array.IndexOf(newVerts, 0), sA.Value);
        Swap(Array.IndexOf(newVerts, 1), sB.Value);

        for (var i = 0; i < 4; i++)
        {
            s0.Vertices[i] = newVerts[i];
            s0.NeighborSimplices[i] = newNeighs[i];
        }

        var isValid = RealMove44.IsValidSite(inspector, targetSimplex, targetEdgeSlot, out var context);

        Assert.True(isValid, $"Branchless validation failed when target edge was forced into LocalEdgeSlot {targetEdgeSlotVal}.");
        Assert.True((context.VA.Value == 0 && context.VB.Value == 1) || (context.VA.Value == 1 && context.VB.Value == 0));
    }
}