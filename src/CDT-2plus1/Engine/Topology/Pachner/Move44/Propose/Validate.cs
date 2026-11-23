using CDT_2plus1.Engine.Topology.Pachner.Move44.Propose;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.Topology.TopologyConstants;

namespace CDT_2plus1.Engine.Topology.Pachner.Move44;

internal readonly partial struct Move44
{
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySelectValidSiteForMove(
        RandomAccessor random,
        InspectingAccessor inspector,
        out Context context)
    {
        var simplexId = random.SelectRandomActiveSimplex();
        var edgeIndex = new LocalEdgeSlot((byte)random.GetRandom(EdgesPerSimplex));

        return IsValidSite(inspector, simplexId, edgeIndex, out context);
    }

    /// <summary>
    /// Validates the strict topological and causal constraints required for a <c>(4,4)</c> spatial edge flip.
    /// Evaluated in <c>O(1)</c> time without allocations or branching loops.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe bool IsValidSite(
        InspectingAccessor inspector,
        SimplexId simplexId,
        LocalEdgeSlot edgeIndex,
        out Context context)
    {
        (var slotA, var slotB) = Tetrahedron.GetEdgeVertexSlots(edgeIndex);

        ref readonly var rootSimplex = ref inspector.GetTetrahedron(simplexId);
        VertexId vA = new(rootSimplex.Vertices[slotA.Value]);
        VertexId vB = new(rootSimplex.Vertices[slotB.Value]);

        var timeA = inspector.GetVertexTime(vA);

        // =================================================================================
        // 1. SPACELIKE EDGE CHECK (Cheapest Fast-Fail)
        // =================================================================================
        // FAST FAIL 1: Causality.
        // A (4,4) move flips a spacelike diagonal (vA, vB) within a single spatial slice.
        // If the randomly selected edge spans two different timeslices (timelike), reject immediately.
        if (timeA != inspector.GetVertexTime(vB))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // =================================================================================
        // 2. EXACT DEGREE CHECK (Graph Traversal)
        // =================================================================================
        // FAST FAIL 2: Wrapping Constraint.
        // The spacelike edge must be shared by EXACTLY 4 simplices forming a bipyramid.
        const int degree = 4;
        Unsafe.SkipInit(out IncidentSimplicesBuffer buffer);
        var incident = (SimplexId*)&buffer;

        if (!inspector.Adjacency.TryGetExactlyNSharedSimplices(vA, vB, incident, degree))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // =================================================================================
        // 3. FOLIATION CHECK (Branchless Type Counting)
        // =================================================================================
        // FAST FAIL 3: Legal CDT Cluster.
        // A valid 4-simplex cluster around a spacelike edge in 2+1D must contain
        // exactly two (3,1) simplices above the slice and two (1,3) simplices below it.
        var t0 = inspector.SimplexIsType(incident[0], SimplexType.ThreeOne); int is31_0 = Unsafe.As<bool, byte>(ref t0);
        var t1 = inspector.SimplexIsType(incident[1], SimplexType.ThreeOne); int is31_1 = Unsafe.As<bool, byte>(ref t1);
        var t2 = inspector.SimplexIsType(incident[2], SimplexType.ThreeOne); int is31_2 = Unsafe.As<bool, byte>(ref t2);
        var t3 = inspector.SimplexIsType(incident[3], SimplexType.ThreeOne); int is31_3 = Unsafe.As<bool, byte>(ref t3);

        if (is31_0 + is31_1 + is31_2 + is31_3 != 2)
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // =================================================================================
        // 4. BRANCHLESS C AND D EXTRACTION (Alternate Equatorial Diagonal)
        // =================================================================================
        // Each incident simplex has two non-edge vertices: one pole on an adjacent timeslice
        // and one equatorial vertex (C or D) on timeA.
        ref readonly var s0 = ref inspector.GetTetrahedron(incident[0]);
        (var w0, var z0) = s0.GetNonEdgeVertices(vA, vB);
        var w0_sp = inspector.GetVertexTime(w0) == timeA; int w0_int = Unsafe.As<bool, byte>(ref w0_sp);
        VertexId C = new((w0_int * w0.Value) + ((1 - w0_int) * z0.Value));

        ref readonly var s1 = ref inspector.GetTetrahedron(incident[1]);
        (var w1, var z1) = s1.GetNonEdgeVertices(vA, vB);
        var w1_sp = inspector.GetVertexTime(w1) == timeA; int w1_int = Unsafe.As<bool, byte>(ref w1_sp);
        VertexId eq1 = new((w1_int * w1.Value) + ((1 - w1_int) * z1.Value));

        ref readonly var s2 = ref inspector.GetTetrahedron(incident[2]);
        (var w2, var z2) = s2.GetNonEdgeVertices(vA, vB);
        var w2_sp = inspector.GetVertexTime(w2) == timeA; int w2_int = Unsafe.As<bool, byte>(ref w2_sp);
        VertexId eq2 = new((w2_int * w2.Value) + ((1 - w2_int) * z2.Value));

        // Vertex D is the spatial vertex opposite C.
        // If eq1 == C, simplex 1 shares C, so D must be eq2; otherwise eq1 is D.
        var eq1_is_C = eq1 == C; int eq1c_int = Unsafe.As<bool, byte>(ref eq1_is_C);
        VertexId D = new(((1 - eq1c_int) * eq1.Value) + (eq1c_int * eq2.Value));

        // =================================================================================
        // 5. TOPOLOGICAL LINK CONDITION (Graph Traversal)
        // =================================================================================
        // FAST FAIL 4: Degeneracy Check.
        // Reject if the proposed new spacelike diagonal (C, D) already exists in the manifold.
        if (inspector.Adjacency.EdgeExists(C, D))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        context = new Context(vA, vB, C, D, incident[0], incident[1], incident[2], incident[3]);
        return true;
    }
}