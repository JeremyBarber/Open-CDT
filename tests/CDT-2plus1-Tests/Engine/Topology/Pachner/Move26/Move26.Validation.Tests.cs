using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.Topology;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

using RealMove26 = CDT_2plus1.Engine.Topology.Pachner.Move26.Move26;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move26;

public sealed unsafe class Move26ValidationTests : IDisposable
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
    public void IsValidSite_GivenPerfect26Cluster_ReturnsTrueAndMapsContext()
    {
        _activeManifold = Move26_62TestArena.CreateValid26Cluster();
        var inspector = new InspectingAccessor(_activeManifold);

        SimplexId target13 = new(1);

        var isValid = RealMove26.IsValidSite(inspector, target13, out var context);

        Assert.True(isValid, "Validation incorrectly rejected a perfect 2->6 setup.");
        Assert.Equal(1, context.Id13.Value);
        Assert.Equal(0, context.Id31.Value);
        Assert.Equal(10, context.TimeEquator.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void IsValidSite_ExhaustivePolePermutations_CorrectlyDecodesAllOrientations(byte poleSlot)
    {
        _activeManifold = Move26_62TestArena.CreateValid26Cluster();
        var raw = (ManifoldOverlay*)_activeManifold;
        var inspector = new InspectingAccessor(_activeManifold);

        SimplexId target13 = new(1);
        ref var s13 = ref raw->SimplexPool[target13.Value];

        const int currentPoleSlot = 3;

        if (poleSlot != currentPoleSlot)
        {
            (s13.Vertices[poleSlot], s13.Vertices[currentPoleSlot]) =
                (s13.Vertices[currentPoleSlot], s13.Vertices[poleSlot]);

            (s13.NeighborSimplices[poleSlot], s13.NeighborSimplices[currentPoleSlot]) =
                (s13.NeighborSimplices[currentPoleSlot], s13.NeighborSimplices[poleSlot]);
        }

        var isValid = RealMove26.IsValidSite(inspector, target13, out var context);

        Assert.True(isValid, $"Branchless pole isolation failed when the pole was in LocalVertexSlot {poleSlot}.");
        Assert.Equal(poleSlot, context.SlotDown.Value);
    }

    [Fact]
    public void IsValidSite_AcrossPeriodicTimeBoundary_ReturnsTrue()
    {
        _activeManifold = Move26_62TestArena.CreateValid26Cluster_PBC();
        var inspector = new InspectingAccessor(_activeManifold);

        var isValid = RealMove26.IsValidSite(inspector, new SimplexId(1), out _);

        Assert.True(isValid, "Rejected a spatial expansion near the periodic temporal boundary.");
    }

    [Fact]
    public void IsValidSite_WhenEquatorNeighborIsInvalidOrNotThreeOne_ReturnsFalse()
    {
        _activeManifold = Move26_62TestArena.CreateValid26Cluster();
        var raw = (ManifoldOverlay*)_activeManifold;
        var inspector = new InspectingAccessor(_activeManifold);

        // Corrupt the neighbor type of Simplex 0 from ThreeOne to TwoTwo
        raw->SimplexTypes[0] = (byte)SimplexType.TwoTwo;
        Assert.False(RealMove26.IsValidSite(inspector, new SimplexId(1), out _));

        // Unlink the equator neighbor to SimplexId.Invalid
        raw->SimplexPool[1].NeighborSimplices[3] = SimplexId.Invalid.Value;
        Assert.False(RealMove26.IsValidSite(inspector, new SimplexId(1), out _));
    }
}