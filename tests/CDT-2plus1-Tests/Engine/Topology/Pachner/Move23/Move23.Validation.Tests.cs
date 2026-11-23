using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

using RealMove23 = CDT_2plus1.Engine.Topology.Pachner.Move23.Move23;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move23;

public sealed unsafe class Move23ValidationTests : IDisposable
{
    private Manifold* _activeManifold;

    public void Dispose()
    {
        if (_activeManifold != null)
            Destroy(ref _activeManifold);
    }

    [Fact]
    public void IsValidSite_GivenPerfect23Cluster_ReturnsTrueAndMapsContext()
    {
        _activeManifold = Move23TestArena.CreateValid23Cluster();
        var inspector = new InspectingAccessor(_activeManifold);

        var isValid = RealMove23.IsValidSite(inspector, new SimplexId(0), new LocalFacetSlot(3), out var context);

        Assert.True(isValid, "Validation incorrectly rejected a perfect (2,3) setup.");
        Assert.Equal(0, context.Id0.Value);
        Assert.Equal(1, context.Id1.Value);
        Assert.Equal(3, context.FaceSlot0.Value);
        Assert.Equal(3, context.FaceSlot1.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void IsValidSite_ExhaustiveFacePermutations_CorrectlyDecodesAllOrientations(byte faceSlot)
    {
        _activeManifold = Move23TestArena.CreateValid23Cluster();
        var raw = (ManifoldOverlay*)_activeManifold;
        var inspector = new InspectingAccessor(_activeManifold);

        SimplexId targetSimplex = new(0);
        ref var s0 = ref raw->SimplexPool[targetSimplex.Value];

        (s0.Vertices[3], s0.Vertices[faceSlot]) = (s0.Vertices[faceSlot], s0.Vertices[3]);
        (s0.NeighborSimplices[3], s0.NeighborSimplices[faceSlot]) = (s0.NeighborSimplices[faceSlot], s0.NeighborSimplices[3]);

        var isValid = RealMove23.IsValidSite(inspector, targetSimplex, new LocalFacetSlot(faceSlot), out _);

        Assert.True(isValid, $"Branchless decoding failed when the target face was in LocalFacetSlot {faceSlot}.");
    }

    [Fact]
    public void IsValidSite_AcrossPeriodicTimeBoundary_ReturnsTrue()
    {
        _activeManifold = Move23TestArena.CreateValid23Cluster_PBC();
        var inspector = new InspectingAccessor(_activeManifold);

        var isValid = RealMove23.IsValidSite(inspector, new SimplexId(0), new LocalFacetSlot(3), out _);

        Assert.True(isValid, "Rejected a timelike face spanning the t_max to t_0 periodic boundary.");
    }

    [Fact]
    public void IsValidSite_GivenMultipleSharedFaces_FailsSimplicialLinkCondition()
    {
        _activeManifold = Move23TestArena.CreateInvalidCluster_MultipleSharedFaces();
        var inspector = new InspectingAccessor(_activeManifold);

        var isValid = RealMove23.IsValidSite(inspector, new SimplexId(0), new LocalFacetSlot(3), out _);

        Assert.False(isValid, "Accepted a site with a multiple-shared-face hinge.");
    }

    [Fact]
    public void IsValidSite_GivenNeighborIsType22_FailsFast()
    {
        _activeManifold = Move23TestArena.CreateInvalidCluster_NeighborIsType22();
        var inspector = new InspectingAccessor(_activeManifold);

        var isValid = RealMove23.IsValidSite(inspector, new SimplexId(0), new LocalFacetSlot(3), out _);

        Assert.False(isValid, "Failed to enforce foliation constraints (neighbor must not be (2,2)).");
    }

    [Fact]
    public void IsValidSite_GivenEdgeAlreadyExists_FailsDegeneracyCheck()
    {
        _activeManifold = Move23TestArena.CreateInvalidCluster_DegenerateEdge();
        var inspector = new InspectingAccessor(_activeManifold);

        var isValid = RealMove23.IsValidSite(inspector, new SimplexId(0), new LocalFacetSlot(3), out _);

        Assert.False(isValid, "Accepted a move that would create a duplicate geometric edge.");
    }
}