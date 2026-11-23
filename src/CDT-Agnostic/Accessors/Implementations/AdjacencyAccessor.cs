using CDT_Agnostic.RuntimeMemory;
using CDT_Agnostic.RuntimeMemory.Adjacency;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_Agnostic.Accessors.Implementations;

/// <summary>
/// Zero-allocation, SIMD-accelerated implementation of <see cref="IAdjacencyAccessor"/> over an unmanaged <see cref="MemoryModel"/>.
/// </summary>
/// <param name="state">Pointer to the underlying unmanaged <see cref="MemoryModel"/> arena.</param>
[StructLayout(LayoutKind.Sequential)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly unsafe struct AdjacencyAccessor(MemoryModel* state) : IAdjacencyAccessor
{
    /// <inheritdoc/>
    public int TotalSimplices
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => state->SimplexCount;
    }

    /// <inheritdoc/>
    public int TotalVertices
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => state->VertexCount;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimplexId GetNthSimplexId(int index) =>
        state->GetSimplexAt(index);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool EdgeExists(VertexId vertexA, VertexId vertexB)
    {
        foreach (var page in state->GetIncidentPages(vertexA))
        {
            for (var i = 0; i < page.Count; i++)
            {
                SimplexId sharedSimplex = new(page.Data[i]);
                if (ContainsIdInPages(vertexB, sharedSimplex))
                    return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TriangleExists(VertexId vertexA, VertexId vertexB, VertexId vertexC)
    {
        foreach (var page in state->GetIncidentPages(vertexA))
        {
            for (var i = 0; i < page.Count; i++)
            {
                SimplexId sharedSimplex = new(page.Data[i]);
                if (ContainsIdInPages(vertexB, sharedSimplex) &&
                    ContainsIdInPages(vertexC, sharedSimplex))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetExactlyNSharedSimplices(VertexId vertexA, VertexId vertexB, SimplexId* buffer, int expected)
    {
        var count = 0;
        foreach (var page in state->GetIncidentPages(vertexA))
        {
            for (var i = 0; i < page.Count; i++)
            {
                SimplexId targetSimplex = new(page.Data[i]);
                if (ContainsIdInPages(vertexB, targetSimplex))
                {
                    if (count == expected)
                        return false;

                    buffer[count++] = targetSimplex;
                }
            }
        }

        return count == expected;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetExactlyNSimplices(VertexId vertexId, SimplexId* buffer, int expected)
    {
        var count = 0;
        foreach (var page in state->GetIncidentPages(vertexId))
        {
            if (count + page.Count > expected)
                return false;

            for (var i = 0; i < page.Count; i++)
            {
                buffer[count++] = new SimplexId(page.Data[i]);
            }
        }

        return count == expected;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ContainsIdInPages(VertexId entity, SimplexId needle)
    {
        foreach (var page in state->GetIncidentPages(entity))
        {
            if (AdjacencyBlock.GetMatchMask(page.Data, page.Count, needle.Value) != 0)
                return true;
        }

        return false;
    }
}