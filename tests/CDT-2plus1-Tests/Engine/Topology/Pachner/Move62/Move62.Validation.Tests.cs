using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

using RealMove62 = CDT_2plus1.Engine.Topology.Pachner.Move62.Move62;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move62;

public sealed unsafe class Move62ValidationTests : IDisposable
{
    private Manifold* _activeManifold;

    public void Dispose()
    {
        if (_activeManifold != null)
        {
            Move26_62TestArena.FreeArena(_activeManifold);
            _activeManifold = null;
        }

        GC.SuppressFinalize(this);
    }

    // =====================================================================================
    // 1. THE HAPPY PATH
    // =====================================================================================
    [Fact]
    public void IsValidSite_GivenPerfect62Cluster_ReturnsTrueAndMapsContext()
    {
        _activeManifold = Move26_62TestArena.CreateValid62Cluster();
        var inspector = new InspectingAccessor(_activeManifold);

        // The central vertex to be deleted is ID 0 in the mock builder
        VertexId targetVertex = new(0);

        var isValid = RealMove62.IsValidSite(inspector, targetVertex, out var context);

        Assert.True(isValid, "Validation incorrectly rejected a perfect 6->2 setup.");
        Assert.Equal(0, context.VTarget.Value);

        // Ensure the context successfully mapped all 6 bounding simplices natively
        Assert.NotEqual(-1, context.Id31_0.Value);
        Assert.NotEqual(-1, context.Id31_1.Value);
        Assert.NotEqual(-1, context.Id31_2.Value);
        Assert.NotEqual(-1, context.Id13_0.Value);
        Assert.NotEqual(-1, context.Id13_1.Value);
        Assert.NotEqual(-1, context.Id13_2.Value);
    }

    // =====================================================================================
    // 2. EXHAUSTIVE PERMUTATIONS (Internal Simplex Vertex Slots)
    // =====================================================================================
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void IsValidSite_ExhaustiveCenterVertexPermutations_CorrectlyIdentifies62(byte centerSlot)
    {
        _activeManifold = Move26_62TestArena.CreateValid62Cluster();

        // Unlocking God-Mode to manually tamper with internal vertex arrays
        var raw = (ManifoldOverlay*)_activeManifold;
        var inspector = new InspectingAccessor(_activeManifold);

        // To ensure validation isn't falsely reliant on the central vertex being 
        // the 0th element inside the constituent tetrahedra arrays, we physically swap it.
        SimplexId target31 = new(0);
        ref var s31 = ref raw->SimplexPool[target31.Value];

        var currentSlot = 0; // In our builder, VCenter is always slot 0
        if (centerSlot != currentSlot)
        {
            (s31.Vertices[centerSlot], s31.Vertices[currentSlot]) =
                (s31.Vertices[currentSlot], s31.Vertices[centerSlot]);

            (s31.NeighborSimplices[centerSlot], s31.NeighborSimplices[currentSlot]) =
                (s31.NeighborSimplices[currentSlot], s31.NeighborSimplices[centerSlot]);
        }

        var isValid = RealMove62.IsValidSite(inspector, new VertexId(0), out var context);

        Assert.True(isValid, $"Validation failed to map the central vertex when shifted to internal LocalVertexSlot {centerSlot}.");
        Assert.Equal(0, context.VTarget.Value);
    }

    // =====================================================================================
    // 3. PERIODIC BOUNDARY CONDITIONS
    // =====================================================================================
    [Fact]
    public void IsValidSite_AcrossPeriodicTimeBoundary_ReturnsTrue()
    {
        _activeManifold = Move26_62TestArena.CreateValid62Cluster_PBC();
        var inspector = new InspectingAccessor(_activeManifold);

        var isValid = RealMove62.IsValidSite(inspector, new VertexId(0), out var context);

        Assert.True(isValid, "Move62 improperly rejected a spatial contraction near the temporal boundary. Ensure time checks respect modulo wrapping.");
        Assert.Equal(0, context.VTarget.Value);
    }

    // =====================================================================================
    // 4. TOPOLOGICAL DEGENERACIES
    // =====================================================================================
    [Fact]
    public void IsValidSite_GivenDegenerateLinkCondition_ReturnsFalse()
    {
        _activeManifold = Move26_62TestArena.CreateInvalid62Cluster_DegenerateLink();
        var inspector = new InspectingAccessor(_activeManifold);

        var isValid = RealMove62.IsValidSite(inspector, new VertexId(0), out _);

        Assert.False(isValid, "Algorithm dangerously accepted a site that violates the spatial link condition (duplicate spatial face).");
    }
}