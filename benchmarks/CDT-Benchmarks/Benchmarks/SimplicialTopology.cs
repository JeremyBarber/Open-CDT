using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.Accessors.Implementations;
using CDT_Agnostic.RuntimeMemory;
using CDT_Agnostic.RuntimeMemory.Identifiers;
using CDT_Agnostic.RuntimeMemory.Native;

using BenchmarkDotNet.Attributes;

namespace CDT_Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks hardware SIMD acceleration against branching scalar loops:
/// (1) 128-bit <c>Vector128&lt;int&gt;</c> + <c>TrailingZeroCount</c> slot lookup on a 32-byte <see cref="Tetrahedron"/>, and
/// (2) 256-bit AVX2 <c>Vector256&lt;int&gt;</c> incidence page intersections across 64-byte <c>AdjacencyBlock</c> cache lines.
/// </summary>
public unsafe class SimdTopology
{
    private Tetrahedron _tet;
    private VertexId _targetVertex;
    private SimplexId _targetNeighbor;

    private MemoryModel* _memory;
    private VertexId _vA;
    private VertexId _vB;
    private VertexId _vC;

    [GlobalSetup]
    public void Setup()
    {
        // 1. Setup 32-byte Tetrahedron with target in slot 3 (worst-case branch depth for scalar search)
        _tet.Vertices[0] = 104;
        _tet.Vertices[1] = 208;
        _tet.Vertices[2] = 315;
        _tet.Vertices[3] = 499;

        _tet.NeighborSimplices[0] = 12;
        _tet.NeighborSimplices[1] = 45;
        _tet.NeighborSimplices[2] = 88;
        _tet.NeighborSimplices[3] = 129;

        _targetVertex = new VertexId(499);
        _targetNeighbor = new SimplexId(129);

        // 2. Setup MemoryModel with realistic 2+1D vertex coordination (14 incident simplices = 1 full 64B cache line)
        _memory = Allocation.Allocate<MemoryModel>(1);
        *_memory = new MemoryModel(maxVertices: 16, maxSimplices: 64, maxAdjacencyBlocks: 32);

        _vA = _memory->AllocVertex();
        _vB = _memory->AllocVertex();
        _vC = _memory->AllocVertex();

        for (var i = 0; i < 14; i++)
        {
            var sId = _memory->AllocSimplex();
            _memory->Link(_vA, sId);

            // Share the final simplex (index 13) across all three vertices
            if (i == 13)
            {
                _memory->Link(_vB, sId);
                _memory->Link(_vC, sId);
            }
            else
            {
                var otherB = _memory->AllocSimplex();
                var otherC = _memory->AllocSimplex();
                _memory->Link(_vB, otherB);
                _memory->Link(_vC, otherC);
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_memory != null)
        {
            _memory->Dispose();
            Allocation.SafeFree(_memory);
            _memory = null;
        }
    }

    // =========================================================================
    // 2A. TETRAHEDRON 128-BIT SIMD vs. SCALAR LOOP
    // =========================================================================

    [Benchmark(Description = "Tetrahedron Slot Lookup: Scalar 4-Step Loop")]
    public int Tetrahedron_ScalarLookup()
    {
        var v = _targetVertex.Value;
        var n = _targetNeighbor.Value;

        var vSlot = 32;
        for (var i = 0; i < 4; i++)
        {
            if (_tet.Vertices[i] == v)
            {
                vSlot = i;
                break;
            }
        }

        var nSlot = 32;
        for (var i = 0; i < 4; i++)
        {
            if (_tet.NeighborSimplices[i] == n)
            {
                nSlot = i;
                break;
            }
        }

        return vSlot + nSlot;
    }

    [Benchmark(Description = "Tetrahedron Slot Lookup: Vector128<int> + TZCNT")]
    public int Tetrahedron_SimdVector128Lookup()
    {
        var vSlot = _tet.FindVertexSlot(_targetVertex);
        var nSlot = _tet.GetFacetFacingSimplexIndex(_targetNeighbor);
        return vSlot.Value + nSlot.Value;
    }

    // =========================================================================
    // 2B. ADJACENCY PAGE INTERSECTION: AVX2 Vector256<int> vs. SCALAR SCAN
    // =========================================================================

    [Benchmark(Description = "Adjacency TriangleExists: Scalar Page Scan")]
    public bool Adjacency_TriangleExists_Scalar()
    {
        foreach (var pageA in _memory->GetIncidentPages(_vA))
        {
            for (var i = 0; i < pageA.Count; i++)
            {
                var candidate = pageA.Data[i];
                if (ScalarContains(_vB, candidate) && ScalarContains(_vC, candidate))
                    return true;
            }
        }

        return false;
    }

    [Benchmark(Description = "Adjacency TriangleExists: AVX2 Vector256<int>")]
    public bool Adjacency_TriangleExists_Avx2()
    {
        var accessor = new AdjacencyAccessor(_memory);
        return accessor.TriangleExists(_vA, _vB, _vC);
    }

    private bool ScalarContains(VertexId vertex, int targetSimplex)
    {
        foreach (var page in _memory->GetIncidentPages(vertex))
        {
            for (var i = 0; i < page.Count; i++)
            {
                if (page.Data[i] == targetSimplex)
                    return true;
            }
        }

        return false;
    }
}