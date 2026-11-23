using CDT_Agnostic.Accessors;
using CDT_Agnostic.Measurements.Observables;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

namespace CDT_Agnostic_Tests.Measurements.Observables;

public sealed class SpectralDimensionTests
{
    // =====================================================================================
    // HOLOGRAPHIC GRAPH ACCESSORS (Deterministic Diffusion + Fragmentation)
    // =====================================================================================

    private readonly unsafe struct MockBouncingAdjacency(int totalSimplices) : IAdjacencyAccessor
    {
        public int TotalSimplices => totalSimplices;
        public int TotalVertices => throw new NotImplementedException();

        public SimplexId GetNthSimplexId(int index) =>
            index switch
            {
                0 => new SimplexId(0),
                1 => new SimplexId(49999),
                2 => new SimplexId(10),
                _ => SimplexId.Invalid
            };

        public bool EdgeExists(VertexId vertexA, VertexId vertexB) => throw new NotImplementedException();
        public bool TriangleExists(VertexId vertexA, VertexId vertexB, VertexId vertexC) => throw new NotImplementedException();
        public bool TryGetExactlyNSharedSimplices(VertexId vertexA, VertexId vertexB, SimplexId* buffer, int expected) => throw new NotImplementedException();
        public bool GetExactlyNSimplices(VertexId vertexId, SimplexId* buffer, int expected) => throw new NotImplementedException();
    }

    /// <summary>
    /// Projects a fragmented dual graph (using slots 0, 49999, and optionally 10)
    /// without allocating unmanaged arena memory.
    /// </summary>
    private readonly struct MockBouncingInspector(int totalSimplices, int connectedFacets = 4)
        : IInspectingAccessor<MockBouncingAdjacency>
    {
        public static int FacetsPerSimplex => 4;
        public static int VerticesPerSimplex => 4;

        public MockBouncingAdjacency Adjacency => new(totalSimplices);

        public SimplexId GetNeighbor(SimplexId simplex, int face)
        {
            if (face >= connectedFacets)
                return SimplexId.Invalid;

            if (totalSimplices == 3)
            {
                return simplex.Value switch
                {
                    0 => new SimplexId(face < 2 ? 49999 : 10),
                    49999 => new SimplexId(face < 2 ? 0 : 10),
                    10 => new SimplexId(face < 2 ? 0 : 49999),
                    _ => SimplexId.Invalid
                };
            }

            return simplex.Value switch
            {
                0 => new SimplexId(49999),
                49999 => new SimplexId(0),
                _ => SimplexId.Invalid
            };
        }

        public VertexId GetVertex(SimplexId simplexId, int vertexIndex) => VertexId.Invalid;
        public TimeSlice GetVertexTime(VertexId vertexId) => throw new NotImplementedException();
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
    public void Measure_EmptyManifold_ReturnsNaNAndZeroProbabilities()
    {
        var mockInspector = new MockBouncingInspector(totalSimplices: 0);
        var mockRandom = new MockRandomAccessor();
        var observable = new SpectralDimension(samplesPerMeasurement: 1, sigmaMax: 6, absoluteArenaCapacity: 16);

        var result = observable.Measure<MockBouncingAdjacency, MockBouncingInspector, MockRandomAccessor>(
            mockInspector, mockRandom);

        Assert.True(double.IsNaN(result.EstimatedDimension));
        Assert.All(result.ReturnProbabilities, p => Assert.Equal(0.0, p));
    }

    [Fact]
    public void Measure_TwoSimplexFragmentedGraph_MatchesExactHeatKernelAndAsymptoticZeroDimension()
    {
        var mockInspector = new MockBouncingInspector(totalSimplices: 2);
        var mockRandom = new MockRandomAccessor();
        const int sigmaMax = 80;

        var observable = new SpectralDimension(
            samplesPerMeasurement: 2,
            sigmaMax: sigmaMax,
            absoluteArenaCapacity: 50000);

        var result = observable.Measure<MockBouncingAdjacency, MockBouncingInspector, MockRandomAccessor>(
            mockInspector, mockRandom);

        // Exact solution on a 2-node 4-regular multigraph for χ = 0.8: P(σ) = 0.5 + 0.5 * (-0.6)^σ
        for (var sigma = 0; sigma <= 10; sigma++)
        {
            var expected = 0.5 + (0.5 * Math.Pow(-0.6, sigma));
            Assert.Equal(expected, result.ReturnProbabilities[sigma], tolerance: 1e-12);
        }

        // Over the [40 .. 80] tail, P(σ) has saturated at 1/N = 0.5 -> slope = 0 -> D_S = 0.0
        Assert.Equal(0.0, result.EstimatedDimension, tolerance: 1e-7);
    }

    [Fact]
    public void Measure_WithBoundaryInvalidNeighbors_ReflectsMassAndConservesProbability()
    {
        // Only 2 of 4 facets connect 0 <-> 49999; the 2 Invalid facets reflect back onto the source simplex,
        // giving effective hold weight 0.2 + 2*(0.8/4) = 0.6 and P(σ) = 0.5 + 0.5 * (0.2)^σ.
        var mockInspector = new MockBouncingInspector(totalSimplices: 2, connectedFacets: 2);
        var mockRandom = new MockRandomAccessor();
        var observable = new SpectralDimension(samplesPerMeasurement: 1, sigmaMax: 6, absoluteArenaCapacity: 50000);

        var result = observable.Measure<MockBouncingAdjacency, MockBouncingInspector, MockRandomAccessor>(
            mockInspector, mockRandom);

        for (var sigma = 0; sigma <= 6; sigma++)
        {
            var expected = 0.5 + (0.5 * Math.Pow(0.2, sigma));
            Assert.Equal(expected, result.ReturnProbabilities[sigma], tolerance: 1e-12);
        }
    }

    [Fact]
    public void Measure_ConsecutiveCallsWithShrinkingActiveSet_IgnoresStaleInactiveBufferSlots()
    {
        var mockRandom = new MockRandomAccessor();
        var observable = new SpectralDimension(samplesPerMeasurement: 1, sigmaMax: 8, absoluteArenaCapacity: 50000);

        // Run 1 populates slot 10 with ~1/3 probability mass on a 3-simplex graph
        var threeNode = new MockBouncingInspector(totalSimplices: 3);
        observable.Measure<MockBouncingAdjacency, MockBouncingInspector, MockRandomAccessor>(threeNode, mockRandom);

        // Run 2 drops slot 10 from the active table without clearing the 50,000-element arena buffers
        var twoNode = new MockBouncingInspector(totalSimplices: 2);
        var result = observable.Measure<MockBouncingAdjacency, MockBouncingInspector, MockRandomAccessor>(twoNode, mockRandom);

        for (var sigma = 0; sigma <= 8; sigma++)
        {
            var expected = 0.5 + (0.5 * Math.Pow(-0.6, sigma));
            Assert.Equal(expected, result.ReturnProbabilities[sigma], tolerance: 1e-12);
        }
    }
}