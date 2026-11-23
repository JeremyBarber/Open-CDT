using CDT_Agnostic.Accessors;
using CDT_Agnostic.Measurements.Observables;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

namespace CDT_Agnostic_Tests.Measurements.Observables;

public sealed class HausdorffDimensionTests
{
    // =====================================================================================
    // HOLOGRAPHIC GRAPH ACCESSORS (Zero-Allocation Mocking)
    // =====================================================================================

    /// <summary>
    /// Minimal unmanaged adjacency lens exposing only the total simplex count required by HausdorffObservable.
    /// </summary>
    private readonly unsafe struct MockDiamondAdjacency(int totalSimplices) : IAdjacencyAccessor
    {
        public int TotalSimplices => totalSimplices;
        public int TotalVertices => throw new NotImplementedException();
        public SimplexId GetNthSimplexId(int index) => throw new NotImplementedException();
        public bool EdgeExists(VertexId vertexA, VertexId vertexB) => throw new NotImplementedException();
        public bool TriangleExists(VertexId vertexA, VertexId vertexB, VertexId vertexC) => throw new NotImplementedException();
        public bool TryGetExactlyNSharedSimplices(VertexId vertexA, VertexId vertexB, SimplexId* buffer, int expected) => throw new NotImplementedException();
        public bool GetExactlyNSimplices(VertexId vertexId, SimplexId* buffer, int expected) => throw new NotImplementedException();
    }

    /// <summary>
    /// A dummy unmanaged struct that fakes an <see cref="IInspectingAccessor{TAdjacency}"/>.
    /// Projects a mathematical 4-node "Diamond" graph (with Simplex 3 placed at fragmented index 49999)
    /// without allocating any unmanaged arena memory.
    /// </summary>
    private readonly struct MockDiamondInspector(int totalSimplices) : IInspectingAccessor<MockDiamondAdjacency>
    {
        public static int FacetsPerSimplex => 4;
        public static int VerticesPerSimplex => 4;

        public MockDiamondAdjacency Adjacency => new(totalSimplices);

        // Fakes the graph topology:
        // 0 connects to 1 and 2
        // 1 connects to 0 and 49999 (fragmented high ID)
        // 2 connects to 0 and 49999 (fragmented high ID)
        // 49999 connects to 1 and 2
        public SimplexId GetNeighbor(SimplexId simplex, int face)
        {
            if (face >= 2)
                return SimplexId.Invalid;

            return simplex.Value switch
            {
                0 => new SimplexId(face == 0 ? 1 : 2),
                1 => new SimplexId(face == 0 ? 0 : 49999),
                2 => new SimplexId(face == 0 ? 0 : 49999),
                49999 => new SimplexId(face == 0 ? 1 : 2),
                _ => SimplexId.Invalid
            };
        }

        public VertexId GetVertex(SimplexId simplexId, int vertexIndex) => VertexId.Invalid;
        public TimeSlice GetVertexTime(VertexId vertexId) => throw new NotImplementedException();
    }

    /// <summary>
    /// A dummy unmanaged struct to fake the Random accessor, pinning the BFS origin to Simplex 0.
    /// </summary>
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
    public void Measure_EmptyManifold_ReturnsNaNAndZeroProfile()
    {
        var mockInspector = new MockDiamondInspector(totalSimplices: 0);
        var mockRandom = new MockRandomAccessor();
        var observable = new Hausdorff(samplesPerMeasurement: 1, maxRadius: 3, absoluteArenaCapacity: 10);

        var result = observable.Measure<MockDiamondAdjacency, MockDiamondInspector, MockRandomAccessor>(
            mockInspector, mockRandom);

        Assert.True(double.IsNaN(result.Dimension));
        Assert.All(result.Profile, count => Assert.Equal(0.0, count));
    }

    [Fact]
    public void Measure_GivenClosedDiamond_CalculatesExactShells()
    {
        // 1. ARRANGE: Instantiate our 4-simplex holographic diamond graph (including high ID 49999)
        var mockInspector = new MockDiamondInspector(totalSimplices: 4);
        var mockRandom = new MockRandomAccessor();

        const int maxRadius = 5;
        var observable = new Hausdorff(samplesPerMeasurement: 1, maxRadius: maxRadius, absoluteArenaCapacity: 50000);

        // 2. ACT
        var result = observable.Measure<MockDiamondAdjacency, MockDiamondInspector, MockRandomAccessor>(
            mockInspector, mockRandom);
        var profile = result.Profile;

        // 3. ASSERT
        // In our diamond, Simplex 0 connects to 2 neighbors (Distance 1),
        // which both converge on Simplex 49999 (Distance 2).
        // Total volume: 1 + 2 + 1 = 4.
        Assert.Equal(1.0, profile[0]); // r=0 (Origin: Simplex 0)
        Assert.Equal(2.0, profile[1]); // r=1 (Simplices 1 & 2)
        Assert.Equal(1.0, profile[2]); // r=2 (Simplex 49999)

        Assert.Equal(0.0, profile[3]); // r=3 (Empty space)
        Assert.Equal(0.0, profile[4]);
        Assert.Equal(0.0, profile[5]);
    }

    [Fact]
    public void Measure_ConsecutiveRuns_MaintainsGenerationalSafety()
    {
        // 1. ARRANGE
        var mockInspector = new MockDiamondInspector(totalSimplices: 4);
        var mockRandom = new MockRandomAccessor();
        var observable = new Hausdorff(samplesPerMeasurement: 1, maxRadius: 5, absoluteArenaCapacity: 50000);

        // 2. ACT
        // Run it twice to prove the _bfsGeneration trick successfully clears the graph
        // without requiring an expensive Array.Clear() on the _visited array.
        observable.Measure<MockDiamondAdjacency, MockDiamondInspector, MockRandomAccessor>(mockInspector, mockRandom);
        var result2 = observable.Measure<MockDiamondAdjacency, MockDiamondInspector, MockRandomAccessor>(
            mockInspector, mockRandom);

        // 3. ASSERT
        // If the generation trick failed, the second run would see all nodes as "visited"
        // and return 0 for everything except the origin.
        Assert.Equal(1.0, result2.Profile[0]);
        Assert.Equal(2.0, result2.Profile[1]);
        Assert.Equal(1.0, result2.Profile[2]);
    }

    [Fact]
    public void Measure_SmallMaxRadius_ClampsRegressionWindowWithoutThrowing()
    {
        // Verifies the rMax clamp when maxRadius < 5 (where rMin + 2 = 5 > maxRadius)
        var mockInspector = new MockDiamondInspector(totalSimplices: 4);
        var mockRandom = new MockRandomAccessor();
        var observable = new Hausdorff(samplesPerMeasurement: 1, maxRadius: 2, absoluteArenaCapacity: 50000);

        var result = observable.Measure<MockDiamondAdjacency, MockDiamondInspector, MockRandomAccessor>(
            mockInspector, mockRandom);

        Assert.Equal([1.0, 2.0, 1.0], result.Profile);
        Assert.True(double.IsNaN(result.Dimension));
    }
}