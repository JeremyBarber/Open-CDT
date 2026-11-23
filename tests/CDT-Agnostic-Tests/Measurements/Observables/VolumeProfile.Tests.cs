using CDT_Agnostic.Accessors;
using CDT_Agnostic.Measurements.Observables;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

namespace CDT_Agnostic_Tests.Measurements.Observables;

public sealed class VolumeProfileTests
{
    // =====================================================================================
    // HOLOGRAPHIC GRAPH ACCESSORS (Volume Profile & Periodic Wrap Mocker)
    // =====================================================================================

    /// <summary>
    /// Simulates a fragmented arena containing exactly 2 active simplices at IDs 0 and 49,999.
    /// </summary>
    private readonly unsafe struct MockVolumeAdjacency(int totalSimplices) : IAdjacencyAccessor
    {
        public int TotalSimplices => totalSimplices;
        public int TotalVertices => throw new NotImplementedException();

        public SimplexId GetNthSimplexId(int index) =>
            index switch
            {
                0 => new SimplexId(0),
                1 => new SimplexId(49999),
                _ => SimplexId.Invalid
            };

        public bool EdgeExists(VertexId vertexA, VertexId vertexB) => throw new NotImplementedException();
        public bool TriangleExists(VertexId vertexA, VertexId vertexB, VertexId vertexC) => throw new NotImplementedException();
        public bool TryGetExactlyNSharedSimplices(VertexId vertexA, VertexId vertexB, SimplexId* buffer, int expected) => throw new NotImplementedException();
        public bool GetExactlyNSimplices(VertexId vertexId, SimplexId* buffer, int expected) => throw new NotImplementedException();
    }

    /// <summary>
    /// Projects two simplices:
    /// - Simplex 0: spans standard interior slices t = 1 and t = 2 (assigned to slab 1).
    /// - Simplex 49999: wraps across the periodic boundary between t = lastSlice and t = 0 (assigned to slab lastSlice).
    /// </summary>
    private readonly struct MockVolumeInspector(int lastSlice, int totalSimplices = 2)
        : IInspectingAccessor<MockVolumeAdjacency>
    {
        public static int FacetsPerSimplex => 4;
        public static int VerticesPerSimplex => 4;

        public MockVolumeAdjacency Adjacency => new(totalSimplices);

        // Encode VertexId as:
        //   Simplex 0     -> vertices 10, 10, 10 (at t = 1) and 20 (at t = 2)
        //   Simplex 49999 -> vertices 30, 30, 30 (at t = lastSlice) and 0 (at t = 0, periodic wrap)
        public VertexId GetVertex(SimplexId simplexId, int vertexIndex) =>
            simplexId.Value switch
            {
                0 => new VertexId(vertexIndex < 3 ? 10 : 20),
                49999 => new VertexId(vertexIndex < 3 ? 30 : 0),
                _ => VertexId.Invalid
            };

        public TimeSlice GetVertexTime(VertexId vertexId) =>
            vertexId.Value switch
            {
                0 => new TimeSlice(0),
                10 => new TimeSlice(1),
                20 => new TimeSlice(2),
                30 => new TimeSlice(lastSlice),
                _ => new TimeSlice(-1)
            };

        public SimplexId GetNeighbor(SimplexId simplexId, int faceIndex) => SimplexId.Invalid;
    }

    private readonly struct MockRandomAccessor : IRandomAccessor
    {
        public SimplexId SelectRandomActiveSimplex() => new(0);
        public uint GetRandomBoundedAtFour() => 0;
        public int GetRandom(int bound) => 0;
        public uint GetRandom() => throw new NotImplementedException();
        public VertexId SelectRandomVertex() => throw new NotImplementedException();
    }

    // =====================================================================================
    // TESTS
    // =====================================================================================

    [Fact]
    public void Measure_EmptyManifold_ReturnsAllZeroSlabs()
    {
        var mockInspector = new MockVolumeInspector(lastSlice: 3, totalSimplices: 0);
        var observable = new VolumeProfile(timeslices: 4);

        var result = observable.Measure<MockVolumeAdjacency, MockVolumeInspector, MockRandomAccessor>(
            mockInspector, default);

        Assert.Equal([0, 0, 0, 0], result);
    }

    [Fact]
    public void Measure_GivenStandardAndPeriodicSimplices_AssignsToCorrectSlabs()
    {
        // 1. ARRANGE: 4 timeslices (t = 0, 1, 2, 3), so the periodic wrap slab is lastSlice = 3
        const int totalTimeslices = 4;
        var mockInspector = new MockVolumeInspector(lastSlice: totalTimeslices - 1);
        var observable = new VolumeProfile(totalTimeslices);

        // 2. ACT
        var result = observable.Measure<MockVolumeAdjacency, MockVolumeInspector, MockRandomAccessor>(
            mockInspector, default);

        // 3. ASSERT
        Assert.Equal(0, result[0]);
        Assert.Equal(1, result[1]); // Simplex 0 spanning t = 1 -> t = 2 assigned to slab 1
        Assert.Equal(0, result[2]);
        Assert.Equal(1, result[3]); // Simplex 49999 wrapping t = 3 -> t = 0 assigned to slab 3
    }

    [Fact]
    public void Measure_AfterHeavyFragmentation_CalculatesTotalVolumeCorrectly()
    {
        // 1. ARRANGE
        const int totalTimeslices = 15;
        var mockInspector = new MockVolumeInspector(lastSlice: totalTimeslices - 1);
        var observable = new VolumeProfile(timeslices: totalTimeslices);

        // 2. ACT
        var result = observable.Measure<MockVolumeAdjacency, MockVolumeInspector, MockRandomAccessor>(
            mockInspector, default);

        // 3. ASSERT: The integrated slab sum must equal the dense active simplex count (2),
        // skipping the 49,998 unallocated slots between ID 0 and ID 49,999.
        var profileSum = 0;
        foreach (var val in result)
            profileSum += val;

        Assert.Equal(2, profileSum);
        Assert.Equal(mockInspector.Adjacency.TotalSimplices, profileSum);
    }
}