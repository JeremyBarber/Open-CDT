using CDT_2plus1.Engine.Topology.Pachner.Move62.Propose;
using CDT_Agnostic.RuntimeMemory.Identifiers;
using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1.Engine.Topology.Pachner.Move62;

/// <summary>
/// Executes the (6,2) Pachner Move for 2+1D Causal Dynamical Triangulations.
/// Safely contracts a targeted spatial vertex entirely out of the manifold, collapsing 
/// a bipyramid of 6 simplices (three (3,1)s and three (1,3)s) back into a single (3,1)/(1,3) pair.
/// </summary>
internal readonly partial struct Move62
{
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySelectValidSiteForMove(RandomAccessor random, InspectingAccessor inspector, out Context context)
    {
        var vTarget = random.SelectRandomVertex();

        return IsValidSite(inspector, vTarget, out context);
    }

    /// <summary>
    /// Validates the strict topological and causal constraints required to safely delete a vertex.
    /// Evaluated in O(1) time entirely without allocations or loops.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe bool IsValidSite(InspectingAccessor inspector, VertexId vTarget, out Context context)
    {
        // =================================================================================
        // 1. MEMORY LOAD & TARGET VALIDATION (Degree & Foliation)
        // =================================================================================
        const int degree = 6;
        Unsafe.SkipInit(out IncidentSimplicesBuffer buffer);
        var incident = (SimplexId*)&buffer;

        // FAST FAIL 1: Degree Constraint.
        // A spatial vertex in a valid (6,2) cluster must have exactly 6 surrounding simplices.
        // If it has 4, 8, etc., it cannot be cleanly collapsed into a single triangle.
        if (!inspector.Adjacency.GetExactlyNSimplices(vTarget, incident, degree))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // 1. Cleanly split into 3x Type31 and 3x Type13
        SimplexId id31_0 = SimplexId.Invalid, id31_1 = SimplexId.Invalid, id31_2 = SimplexId.Invalid;
        SimplexId id13_0 = SimplexId.Invalid, id13_1 = SimplexId.Invalid, id13_2 = SimplexId.Invalid;
        int count31 = 0, count13 = 0;

        for (var i = 0; i < degree; i++)
        {
            var id = incident[i];
            if (inspector.SimplexIsType(id, SimplexType.ThreeOne))
            {
                if (count31 == 0) id31_0 = id; else if (count31 == 1) id31_1 = id; else id31_2 = id;
                count31++;
            }
            else if (inspector.SimplexIsType(id, SimplexType.OneThree))
            {
                if (count13 == 0) id13_0 = id; else if (count13 == 1) id13_1 = id; else id13_2 = id;
                count13++;
            }
        }

        // FAST FAIL 2: Cluster Symmetry.
        // The cluster must form a perfect bipyramid divided by the spatial equator:
        // exactly 3 upward-facing (3,1) simplices and 3 downward-facing (1,3) simplices.
        if (count31 != 3 || count13 != 3)
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        ref readonly var s31_0 = ref inspector.GetTetrahedron(id31_0);
        ref readonly var s31_1 = ref inspector.GetTetrahedron(id31_1);
        ref readonly var s13_0 = ref inspector.GetTetrahedron(id13_0);

        var tTarget = inspector.GetVertexTime(vTarget);

        // =====================================================================================
        // 2. BRANCHLESS POLE DEDUCTION & CAUSALITY GUARD
        // =====================================================================================
        // CRITICAL TOPOLOGY FIX: vTarget MUST be on the spatial equator.
        // By CDT definitions, a (3,1) simplex has 3 vertices on the equator and 1 Top Pole.
        // If vTarget is truly on the equator, exactly ONE other vertex in this simplex will have 
        // a different time slice (the Top Pole). 
        // If vTarget was actually the pole (a fatal selection error), THREE vertices would have a different time slice.

        VertexId u0 = new(s31_0.Vertices[0]); VertexId u1 = new(s31_0.Vertices[1]);
        VertexId u2 = new(s31_0.Vertices[2]); VertexId u3 = new(s31_0.Vertices[3]);

        var isU0 = inspector.GetVertexTime(u0) != tTarget ? (byte)1 : (byte)0;
        var isU1 = inspector.GetVertexTime(u1) != tTarget ? (byte)1 : (byte)0;
        var isU2 = inspector.GetVertexTime(u2) != tTarget ? (byte)1 : (byte)0;
        var isU3 = inspector.GetVertexTime(u3) != tTarget ? (byte)1 : (byte)0;

        // FAST FAIL 3: Causality Guard (Upper Hemisphere).
        // If the sum is not exactly 1, vTarget is not on the equator, and deleting it would tear the manifold foliation.
        if (isU0 + isU1 + isU2 + isU3 != 1)
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // Branchless Multiplexer: Extracts the ID of the exact Top Pole.
        VertexId U = new((isU0 * u0.Value) + (isU1 * u1.Value) + (isU2 * u2.Value) + (isU3 * u3.Value));

        VertexId v0 = new(s13_0.Vertices[0]); VertexId v1 = new(s13_0.Vertices[1]);
        VertexId v2 = new(s13_0.Vertices[2]); VertexId v3 = new(s13_0.Vertices[3]);

        var isV0 = inspector.GetVertexTime(v0) != tTarget ? (byte)1 : (byte)0;
        var isV1 = inspector.GetVertexTime(v1) != tTarget ? (byte)1 : (byte)0;
        var isV2 = inspector.GetVertexTime(v2) != tTarget ? (byte)1 : (byte)0;
        var isV3 = inspector.GetVertexTime(v3) != tTarget ? (byte)1 : (byte)0;

        // FAST FAIL 4: Causality Guard (Lower Hemisphere). Ensures symmetry across the slab.
        if (isV0 + isV1 + isV2 + isV3 != 1)
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        VertexId V = new((isV0 * v0.Value) + (isV1 * v1.Value) + (isV2 * v2.Value) + (isV3 * v3.Value));

        // =====================================================================================
        // 3. BRANCHLESS EQUATOR DEDUCTION (A, B, C)
        // =====================================================================================
        // The timelike axis of s31_0 is defined by (vTarget, U). Therefore, the remaining two 
        // vertices in the tetrahedron MUST be spatial equatorial vertices (A and B).
        (var A, var B) = s31_0.GetNonEdgeVertices(vTarget, U);

        // s31_1 shares a face with s31_0, so it shares exactly one of those equatorial vertices 
        // (A or B) and introduces the final unique equatorial vertex (C) to close the triangle.
        (var w, var z) = s31_1.GetNonEdgeVertices(vTarget, U);

        // Branchless multiplexer: If w is A or B, we already know it, so C must be z. 
        // Otherwise, C must be w.
        var hasA_or_B_in_w = (w == A || w == B) ? (byte)1 : (byte)0;
        VertexId C = new((hasA_or_B_in_w * z.Value) + ((1 - hasA_or_B_in_w) * w.Value));

        // =====================================================================================
        // 4. TOPOLOGICAL LINK CONDITION (Graph Traversal)
        // =====================================================================================
        // FAST FAIL 5: Degeneracy Check.
        // The (6,2) move deletes vTarget and pinches the surrounding spatial hexagon into a single 
        // spatial triangle (A, B, C). If this exact triangle already exists elsewhere in the 
        // manifold, creating it here folds the universe on itself, creating a non-manifold singularity.
        if (inspector.Adjacency.TriangleExists(A, B, C))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // Success! Pass the fully verified structure to the mutator.
        context = new Context(vTarget, U, V, A, B, C, id31_0, id31_1, id31_2, id13_0, id13_1, id13_2);
        return true;
    }
}