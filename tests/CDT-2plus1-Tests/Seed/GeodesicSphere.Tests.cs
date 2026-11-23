using CDT_2plus1.Seed;
using Xunit;

namespace CDT_2plus1_Tests.Seed;

public sealed class GeodesicSphereTests
{
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(5u)]
    public void Build_SatisfiesSphericalEulerCharacteristicAndVertexDegreeInvariants(uint k)
    {
        (var vertexCount, var triangles) = GeodesicSphere.Build(k);

        var expectedVertices = (int)(10 * k * k + 2);
        var expectedFaces = (int)(20 * k * k);
        var expectedEdges = (int)(30 * k * k);

        Assert.Equal(expectedVertices, vertexCount);
        Assert.Equal(expectedFaces, triangles.Length);

        var edges = new HashSet<(int, int)>();
        var degree = new int[vertexCount];

        foreach ((var a, var b, var c) in triangles)
        {
            Assert.NotEqual(a, b);
            Assert.NotEqual(b, c);
            Assert.NotEqual(a, c);

            AddEdge(a, b);
            AddEdge(b, c);
            AddEdge(a, c);
        }

        void AddEdge(int u, int v)
        {
            var min = Math.Min(u, v);
            var max = Math.Max(u, v);
            if (edges.Add((min, max)))
            {
                degree[min]++;
                degree[max]++;
            }
        }

        Assert.Equal(expectedEdges, edges.Count);

        // Euler characteristic of S^2: V - E + F == 2
        Assert.Equal(2, vertexCount - edges.Count + triangles.Length);

        // Exactly 12 icosahedral poles have degree 5; all remaining (10k^2 - 10) vertices have degree 6
        var deg5Count = 0;
        var deg6Count = 0;
        for (var v = 0; v < vertexCount; v++)
        {
            if (degree[v] == 5) deg5Count++;
            else if (degree[v] == 6) deg6Count++;
            else Assert.Fail($"Unexpected spatial vertex degree {degree[v]} at vertex {v}.");
        }

        Assert.Equal(12, deg5Count);
        Assert.Equal(expectedVertices - 12, deg6Count);
    }

    [Fact]
    public void Build_WithZeroSubdivisions_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeodesicSphere.Build(0));
    }
}