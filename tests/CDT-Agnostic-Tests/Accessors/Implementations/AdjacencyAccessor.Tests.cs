using CDT_Agnostic.Accessors.Implementations;
using CDT_Agnostic.RuntimeMemory;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

namespace CDT_Agnostic_Tests.Accessors.Implementations;

public sealed unsafe class AdjacencyAccessorTests : IDisposable
{
    private MemoryModel _memory = new(maxVertices: 64, maxSimplices: 128, maxAdjacencyBlocks: 64);

    public void Dispose() => _memory.Dispose();

    [Fact]
    public void CountsAndDenseIndexing_TrackAllocatedEntitiesCorrectly()
    {
        fixed (MemoryModel* ptr = &_memory)
        {
            var accessor = new AdjacencyAccessor(ptr);

            Assert.Equal(0, accessor.TotalVertices);
            Assert.Equal(0, accessor.TotalSimplices);

            var v0 = _memory.AllocVertex();
            var v1 = _memory.AllocVertex();
            var s0 = _memory.AllocSimplex();
            var s1 = _memory.AllocSimplex();
            var s2 = _memory.AllocSimplex();

            Assert.Equal(2, accessor.TotalVertices);
            Assert.Equal(3, accessor.TotalSimplices);

            var active = new HashSet<SimplexId>
            {
                accessor.GetNthSimplexId(0),
                accessor.GetNthSimplexId(1),
                accessor.GetNthSimplexId(2)
            };
            Assert.Contains(s0, active);
            Assert.Contains(s1, active);
            Assert.Contains(s2, active);

            _memory.FreeSimplex(s1);
            Assert.Equal(2, accessor.TotalSimplices);
        }
    }

    [Theory]
    [InlineData(1)]   // Scalar tail only
    [InlineData(4)]   // Exact Vector128 boundary
    [InlineData(7)]   // Vector128 + 3 scalar tail
    [InlineData(8)]   // Exact Vector256 boundary
    [InlineData(12)]  // Vector256 (8) + Vector128 (4)
    [InlineData(15)]  // Full single AdjacencyBlock (8 + 4 + 3)
    [InlineData(16)]  // Spills into a 2nd AdjacencyBlock page (15 + 1)
    [InlineData(35)]  // Spills across 3 AdjacencyBlock pages (15 + 15 + 5)
    public void EdgeExists_AcrossAllSimdAndMultiPageBoundaries_FindsTargetAtEveryPosition(int incidentCount)
    {
        fixed (MemoryModel* ptr = &_memory)
        {
            var accessor = new AdjacencyAccessor(ptr);

            var vA = _memory.AllocVertex();
            var vB = _memory.AllocVertex();
            var vUnconnected = _memory.AllocVertex();

            var simplicesOnB = new SimplexId[incidentCount];
            for (var i = 0; i < incidentCount; i++)
            {
                simplicesOnB[i] = _memory.AllocSimplex();
                _memory.Link(vB, simplicesOnB[i]);
            }

            Assert.False(accessor.EdgeExists(vA, vB));

            // Link vA to each simplex on vB one at a time to test matching at index 0 .. incidentCount-1
            for (var targetIdx = 0; targetIdx < incidentCount; targetIdx++)
            {
                _memory.Link(vA, simplicesOnB[targetIdx]);
                Assert.True(accessor.EdgeExists(vA, vB));
                Assert.True(accessor.EdgeExists(vB, vA));
                Assert.False(accessor.EdgeExists(vA, vUnconnected));
                _memory.Unlink(vA, simplicesOnB[targetIdx]);
                Assert.False(accessor.EdgeExists(vA, vB));
            }
        }
    }

    [Fact]
    public void TriangleExists_RequiresAllThreeVerticesToShareAtLeastOneSimplex()
    {
        fixed (MemoryModel* ptr = &_memory)
        {
            var accessor = new AdjacencyAccessor(ptr);

            var vA = _memory.AllocVertex();
            var vB = _memory.AllocVertex();
            var vC = _memory.AllocVertex();

            var sAB = _memory.AllocSimplex();
            var sBC = _memory.AllocSimplex();
            var sCA = _memory.AllocSimplex();

            // Pairwise edges exist (AB, BC, CA), but no single simplex contains all three
            _memory.Link(vA, sAB); _memory.Link(vB, sAB);
            _memory.Link(vB, sBC); _memory.Link(vC, sBC);
            _memory.Link(vC, sCA); _memory.Link(vA, sCA);

            Assert.True(accessor.EdgeExists(vA, vB));
            Assert.True(accessor.EdgeExists(vB, vC));
            Assert.True(accessor.EdgeExists(vC, vA));
            Assert.False(accessor.TriangleExists(vA, vB, vC));

            // Now link a simplex shared by all three vertices
            var sABC = _memory.AllocSimplex();
            _memory.Link(vA, sABC);
            _memory.Link(vB, sABC);
            _memory.Link(vC, sABC);

            Assert.True(accessor.TriangleExists(vA, vB, vC));
        }
    }

    [Fact]
    public void TryGetExactlyNSharedSimplices_EnforcesExactCountAndDoesNotOverflowBuffer()
    {
        fixed (MemoryModel* ptr = &_memory)
        {
            var accessor = new AdjacencyAccessor(ptr);

            var vA = _memory.AllocVertex();
            var vB = _memory.AllocVertex();

            var s0 = _memory.AllocSimplex();
            var s1 = _memory.AllocSimplex();
            var s2 = _memory.AllocSimplex();
            var sOnlyA = _memory.AllocSimplex();

            _memory.Link(vA, s0); _memory.Link(vB, s0);
            _memory.Link(vA, s1); _memory.Link(vB, s1);
            _memory.Link(vA, s2); _memory.Link(vB, s2);
            _memory.Link(vA, sOnlyA);

            var buffer = stackalloc SimplexId[4];
            SimplexId sentinel = new(99999);
            buffer[2] = sentinel;
            buffer[3] = sentinel;

            // Too few expected (2 when 3 exist): must return false and never overwrite buffer[2]
            Assert.False(accessor.TryGetExactlyNSharedSimplices(vA, vB, buffer, expected: 2));
            Assert.Equal(sentinel, buffer[2]);

            // Too many expected (4 when 3 exist): must return false
            Assert.False(accessor.TryGetExactlyNSharedSimplices(vA, vB, buffer, expected: 4));

            // Exact match (3): returns true and populates buffer[0..2]
            Assert.True(accessor.TryGetExactlyNSharedSimplices(vA, vB, buffer, expected: 3));
            Assert.Equal(sentinel, buffer[3]);

            var extracted = new HashSet<SimplexId> { buffer[0], buffer[1], buffer[2] };
            Assert.Contains(s0, extracted);
            Assert.Contains(s1, extracted);
            Assert.Contains(s2, extracted);
        }
    }

    [Fact]
    public void GetExactlyNSimplices_AcrossMultiPageChain_EnforcesExactCountAndSafetyBounds()
    {
        fixed (MemoryModel* ptr = &_memory)
        {
            var accessor = new AdjacencyAccessor(ptr);
            var vCenter = _memory.AllocVertex();

            // Link 20 simplices so the vertex spans 2 AdjacencyBlock pages (15 + 5)
            const int totalLinked = 20;
            var expectedSet = new HashSet<SimplexId>();
            for (var i = 0; i < totalLinked; i++)
            {
                var s = _memory.AllocSimplex();
                _memory.Link(vCenter, s);
                expectedSet.Add(s);
            }

            var buffer = stackalloc SimplexId[21];
            SimplexId sentinel = new(77777);
            buffer[20] = sentinel;

            // Asking for fewer (6) or more (21) fails fast
            Assert.False(accessor.GetExactlyNSimplices(vCenter, buffer, expected: 6));
            Assert.False(accessor.GetExactlyNSimplices(vCenter, buffer, expected: 21));

            // Asking for exact count (20) succeeds across both pages without touching buffer[20]
            Assert.True(accessor.GetExactlyNSimplices(vCenter, buffer, expected: totalLinked));
            Assert.Equal(sentinel, buffer[20]);

            var actualSet = new HashSet<SimplexId>();
            for (var i = 0; i < totalLinked; i++)
            {
                actualSet.Add(buffer[i]);
            }

            Assert.Equal(expectedSet, actualSet);
        }
    }
}