using CDT_2plus1.Engine.Topology.Pachner.Move32.Propose;
using CDT_Agnostic.RuntimeMemory.Identifiers;
using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.Topology.TopologyConstants;

namespace CDT_2plus1.Engine.Topology.Pachner.Move32;

/// <summary>
/// Executes the (3,2) Pachner Move for 2+1D Causal Dynamical Triangulations.
/// Safely collapses a timelike edge shared by exactly 3 simplices (two (2,2)s and one asymmetric)
/// into a single timelike triangle, contracting the geometry from 3 simplices down to 2.
/// </summary>
internal readonly partial struct Move32
{
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool TrySelectValidSiteForMove(RandomAccessor random, InspectingAccessor inspector, out Context context)
    {
        var simplexId = random.SelectRandomType22();
        LocalEdgeSlot localEdgeId = new((byte)random.GetRandom(EdgesPerSimplex));

        return IsValidSite(inspector, simplexId, localEdgeId, out context);
    }

    /// <summary>
    /// Validates the strict topological and causal constraints required for a (3,2) move.
    /// Evaluated in O(1) time entirely without allocations or loops.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool IsValidSite(InspectingAccessor inspector, SimplexId simplexId, LocalEdgeSlot localEdgeId, out Context context)
    {
        ref readonly var rootSimplex = ref inspector.GetTetrahedron(simplexId);
        (var slotA, var slotB) = Tetrahedron.GetEdgeVertexSlots(localEdgeId);

        VertexId vA = new(rootSimplex.Vertices[slotA.Value]);
        VertexId vB = new(rootSimplex.Vertices[slotB.Value]);

        // =================================================================================
        // 1. TIMELIKE CHECK (Cheapest Fast-Fail)
        // =================================================================================
        // FAST FAIL 1: Causality.
        // A (3,2) move can only collapse a timelike axis spanning t and t+1. 
        // We have a 33% chance of randomly selecting a spacelike edge on a (2,2) simplex. 
        // We evaluate this equality instantly before touching the adjacency graph.
        if (inspector.GetVertexTime(vA) == inspector.GetVertexTime(vB))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // =================================================================================
        // 2. EXACT DEGREE CHECK (Graph Traversal)
        // =================================================================================
        // FAST FAIL 2: Wrapping Constraint.
        // The edge must be completely enclosed by EXACTLY 3 simplices.
        const int degree = 3;
        Unsafe.SkipInit(out IncidentSimplicesBuffer buffer);
        var incident = (SimplexId*)&buffer;

        // TryGetExactlyNSharedSimplices traverses the wheel around the edge.
        // It safely aborts if the wheel closes in < 3 or spans > 3 simplices.
        if (!inspector.Adjacency.TryGetExactlyNSharedSimplices(vA, vB, incident, degree))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        SimplexId id0 = incident[0], id1 = incident[1], id2 = incident[2];

        // =================================================================================
        // 3. FOLIATION CHECK (Branchless Type Counting)
        // =================================================================================
        // FAST FAIL 3: Legal CDT Cluster.
        // A legal 3->2 cluster must contain exactly two (2,2)s and one Asymmetric ((3,1)/(1,3)).
        var t0_is22 = inspector.SimplexIsType(id0, SimplexType.TwoTwo);
        var t1_is22 = inspector.SimplexIsType(id1, SimplexType.TwoTwo);
        var t2_is22 = inspector.SimplexIsType(id2, SimplexType.TwoTwo);

        // Reinterpret the booleans directly as 0 or 1 bytes with zero instruction cost.
        int is22_0 = Unsafe.As<bool, byte>(ref t0_is22);
        int is22_1 = Unsafe.As<bool, byte>(ref t1_is22);
        int is22_2 = Unsafe.As<bool, byte>(ref t2_is22);

        if ((is22_0 + is22_1 + is22_2) != 2)
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // =================================================================================
        // 4. ASYMMETRIC SIMPLEX DEDUCTION (Single-Cycle Bitwise Masking)
        // =================================================================================
        // We must extract the ID of the single simplex that is NOT a (2,2).
        // We subtract 1 from the 0/1 type states to create hardware-level bitmasks:
        // If is22 == 1: mask = 0  (0x00000000). (id & mask) zeros out the unwanted Type22 ID.
        // If is22 == 0: mask = -1 (0xFFFFFFFF). (id & mask) preserves the wanted Asym ID.
        var mask0 = is22_0 - 1;
        var mask1 = is22_1 - 1;
        var mask2 = is22_2 - 1;

        SimplexId idAsym = new((id0.Value & mask0) | (id1.Value & mask1) | (id2.Value & mask2));

        // =================================================================================
        // 5. BOUNDING TRIANGLE EXTRACTION (O(1) Branchless Intersection)
        // =================================================================================
        // The 3 simplices form a tube around the central axis, bounded by vertices (P, Q, R).
        // Upon collapsing the axis, this boundary forms the new shared timelike face.
        // We extract the non-axial vertices from the first two simplices. They are guaranteed 
        // to share exactly one vertex, and hold one unique vertex each.
        ref readonly var s0 = ref inspector.GetTetrahedron(id0);
        ref readonly var s1 = ref inspector.GetTetrahedron(id1);

        (var p, var q) = s0.GetNonEdgeVertices(vA, vB);
        (var w, var z) = s1.GetNonEdgeVertices(vA, vB);

        var wIsP = w == p;
        var wIsQ = w == q;
        var wIsKnown = Unsafe.As<bool, byte>(ref wIsP) | Unsafe.As<bool, byte>(ref wIsQ);

        // We isolate the 3rd unique vertex (R).
        // If w is known (1): maskW = 0. We want z. (~maskW & z) | (maskW & w) -> z.
        // If w is unknown (0): maskW = -1. We want w. (~maskW & z) | (maskW & w) -> w.
        var maskW = wIsKnown - 1;
        VertexId r = new((z.Value & ~maskW) | (w.Value & maskW));

        // =================================================================================
        // 6. TOPOLOGICAL LINK CONDITION (Graph Traversal)
        // =================================================================================
        // FAST FAIL 4: Degeneracy Check.
        // Collapsing the axis will create a new physical face (P, Q, R). If that face already 
        // exists somewhere else in the manifold, creating it here folds the universe on itself.
        if (inspector.Adjacency.TriangleExists(p, q, r))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // Notice: The individual cluster IDs (id0, id1, id2) are passed purely to tear them down.
        // The mapping logic downstream only cares about idAsym.
        context = new Context(id0, id1, id2, idAsym, vA, vB, p, q, r);
        return true;
    }
}