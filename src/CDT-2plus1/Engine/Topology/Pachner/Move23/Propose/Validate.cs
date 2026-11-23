using CDT_2plus1.Engine.Topology.Pachner.Move23.Propose;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1.Engine.Topology.Pachner.Move23;

internal readonly partial struct Move23
{
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySelectValidSiteForMove(
        RandomAccessor random,
        InspectingAccessor inspector,
        out Context context)
    {
        var simplexId = random.SelectRandomType22();
        var localFaceId = new LocalFacetSlot((byte)random.GetRandomBoundedAtFour());

        return IsValidSite(inspector, simplexId, localFaceId, out context);
    }

    /// <summary>
    /// Validates the topological, simplicial-link, and causal constraints required to execute a <c>(2,3)</c> move.
    /// Evaluated in <c>O(1)</c> time with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe bool IsValidSite(
        InspectingAccessor inspector,
        SimplexId simplexId,
        LocalFacetSlot localFaceId,
        out Context context)
    {
        // =================================================================================
        // 1. MEMORY LOAD & FOLIATION VALIDATION
        // =================================================================================
        ref readonly var s0 = ref inspector.GetTetrahedron(simplexId);
        SimplexId id1 = new(s0.NeighborSimplices[localFaceId.Value]);

        // FAST FAIL 1: Foliation Constraint.
        // A 2+1D (2,3) move strictly requires one (2,2) simplex and one asymmetric ((3,1) or (1,3)) simplex.
        if (inspector.SimplexIsType(id1, SimplexType.TwoTwo))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // =================================================================================
        // 2. SIMPLICIAL LINK CONDITION (Local Register Checks)
        // =================================================================================
        // FAST FAIL 2: Reject local boundary self-folding (a simplex sharing >= 2 faces with the same neighbor).
        ref readonly var s1 = ref inspector.GetTetrahedron(id1);

        var s0Degenerate =
            (s0.NeighborSimplices[0] == s0.NeighborSimplices[1]) |
            (s0.NeighborSimplices[0] == s0.NeighborSimplices[2]) |
            (s0.NeighborSimplices[0] == s0.NeighborSimplices[3]) |
            (s0.NeighborSimplices[1] == s0.NeighborSimplices[2]) |
            (s0.NeighborSimplices[1] == s0.NeighborSimplices[3]) |
            (s0.NeighborSimplices[2] == s0.NeighborSimplices[3]);

        var s1Degenerate =
            (s1.NeighborSimplices[0] == s1.NeighborSimplices[1]) |
            (s1.NeighborSimplices[0] == s1.NeighborSimplices[2]) |
            (s1.NeighborSimplices[0] == s1.NeighborSimplices[3]) |
            (s1.NeighborSimplices[1] == s1.NeighborSimplices[2]) |
            (s1.NeighborSimplices[1] == s1.NeighborSimplices[3]) |
            (s1.NeighborSimplices[2] == s1.NeighborSimplices[3]);

        if (s0Degenerate | s1Degenerate)
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // =================================================================================
        // 3. POLE EXTRACTION & CAUSALITY CHECK
        // =================================================================================
        VertexId vTarget = new(s0.Vertices[localFaceId.Value]);

        var backSlot1 = s1.GetFacetFacingSimplexIndex(simplexId);
        VertexId vNeighbour = new(s1.Vertices[backSlot1.Value]);

        // FAST FAIL 3: Causality Constraint.
        // The newly inserted central edge connecting the poles must be strictly timelike.
        if (inspector.GetVertexTime(vTarget) == inspector.GetVertexTime(vNeighbour))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // =================================================================================
        // 4. GLOBAL MANIFOLD LINK CHECK
        // =================================================================================
        // FAST FAIL 4: Reject if the proposed timelike edge (vTarget, vNeighbour) already exists in the manifold.
        if (inspector.Adjacency.EdgeExists(vTarget, vNeighbour))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // =================================================================================
        // 5. CONTEXT MAPPING
        // =================================================================================
        context = new Context(simplexId, id1, localFaceId, backSlot1, vTarget, vNeighbour);
        return true;
    }
}