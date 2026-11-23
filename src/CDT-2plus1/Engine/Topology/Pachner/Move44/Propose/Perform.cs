using CDT_2plus1.Engine.Topology.Pachner.Move44.Propose;
using CDT_Agnostic.RuntimeMemory.Identifiers;
using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1.Engine.Topology.Pachner.Move44;

internal readonly partial struct Move44
{
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe void PerformMove(MutatingAccessor mutator, in Context context)
    {
        // 0. Cache-friendly extraction of the 4 root tetrahedra
        ref readonly var s0 = ref mutator.Inspect.GetTetrahedron(context.Id0);
        ref readonly var s1 = ref mutator.Inspect.GetTetrahedron(context.Id1);
        ref readonly var s2 = ref mutator.Inspect.GetTetrahedron(context.Id2);
        ref readonly var s3 = ref mutator.Inspect.GetTetrahedron(context.Id3);

        // =====================================================================================
        // 1. UNROLLED BRANCHLESS SEMANTIC DECODING (Role Assignment)
        // =====================================================================================
        // ALGEBRAIC PROOF:
        // The 4 incident simplices arrive in an arbitrary, unpredictable array order. 
        // To wire the new geometry safely, we must strictly classify them into 4 physical roles:
        // - idC31: The (3,1) simplex containing spatial vertex C.
        // - idD31: The (3,1) simplex containing spatial vertex D.
        // - idC13: The (1,3) simplex containing spatial vertex C.
        // - idD13: The (1,3) simplex containing spatial vertex D.
        // 
        // We instantly evaluate their foliation type (3,1 vs 1,3) and containment of C as 1 or 0.
        // By multiplying the IDs by these binary states, we mathematically filter and route 
        // the exact ID to its proper role in O(1) time, avoiding slow conditional branches.
        var t0 = mutator.Inspect.SimplexIsType(context.Id0, SimplexType.ThreeOne); int is31_0 = Unsafe.As<bool, byte>(ref t0);
        var t1 = mutator.Inspect.SimplexIsType(context.Id1, SimplexType.ThreeOne); int is31_1 = Unsafe.As<bool, byte>(ref t1);
        var t2 = mutator.Inspect.SimplexIsType(context.Id2, SimplexType.ThreeOne); int is31_2 = Unsafe.As<bool, byte>(ref t2);
        var t3 = mutator.Inspect.SimplexIsType(context.Id3, SimplexType.ThreeOne); int is31_3 = Unsafe.As<bool, byte>(ref t3);

        var c0 = s0.FindVertexSlot(context.C).Value < 4; int hasC_0 = Unsafe.As<bool, byte>(ref c0);
        var c1 = s1.FindVertexSlot(context.C).Value < 4; int hasC_1 = Unsafe.As<bool, byte>(ref c1);
        var c2 = s2.FindVertexSlot(context.C).Value < 4; int hasC_2 = Unsafe.As<bool, byte>(ref c2);
        var c3 = s3.FindVertexSlot(context.C).Value < 4; int hasC_3 = Unsafe.As<bool, byte>(ref c3);

        SimplexId idC31 = new(
            ((is31_0 & hasC_0) * context.Id0.Value) + ((is31_1 & hasC_1) * context.Id1.Value) +
            ((is31_2 & hasC_2) * context.Id2.Value) + ((is31_3 & hasC_3) * context.Id3.Value));

        SimplexId idD31 = new(
            ((is31_0 & (1 - hasC_0)) * context.Id0.Value) + ((is31_1 & (1 - hasC_1)) * context.Id1.Value) +
            ((is31_2 & (1 - hasC_2)) * context.Id2.Value) + ((is31_3 & (1 - hasC_3)) * context.Id3.Value));

        SimplexId idC13 = new(
            (((1 - is31_0) & hasC_0) * context.Id0.Value) + (((1 - is31_1) & hasC_1) * context.Id1.Value) +
            (((1 - is31_2) & hasC_2) * context.Id2.Value) + (((1 - is31_3) & hasC_3) * context.Id3.Value));

        SimplexId idD13 = new(
            (((1 - is31_0) & (1 - hasC_0)) * context.Id0.Value) + (((1 - is31_1) & (1 - hasC_1)) * context.Id1.Value) +
            (((1 - is31_2) & (1 - hasC_2)) * context.Id2.Value) + (((1 - is31_3) & (1 - hasC_3)) * context.Id3.Value));
        // =====================================================================================
        // 2. MATHEMATICAL POLE DEDUCTION (U and V)
        // =====================================================================================
        // GEOMETRIC CONTEXT:
        // The four simplices form a bipyramid around the shared spacelike diagonal (VA, VB).
        // The spatial equator is the quadrilateral (VA, C, VB, D), while U (top) and V (bottom)
        // are the temporal poles on the adjacent time slices.
        // Because idC31 is a (3,1) simplex containing the equatorial triangle (VA, VB, C),
        // its 4th vertex MUST be the top temporal pole U. Similarly, idC13's 4th vertex is V.
        ref readonly var sC31 = ref mutator.Inspect.GetTetrahedron(idC31);
        (var w31, var z31) = sC31.GetNonEdgeVertices(context.VA, context.VB);
        var wIsC_31 = w31 == context.C; int wIsC_31_int = Unsafe.As<bool, byte>(ref wIsC_31);
        VertexId U = new((wIsC_31_int * z31.Value) + ((1 - wIsC_31_int) * w31.Value)); // If w is C, z is Pole U.

        ref readonly var sC13 = ref mutator.Inspect.GetTetrahedron(idC13);
        (var w13, var z13) = sC13.GetNonEdgeVertices(context.VA, context.VB);
        var wIsC_13 = w13 == context.C; int wIsC_13_int = Unsafe.As<bool, byte>(ref wIsC_13);
        VertexId V = new((wIsC_13_int * z13.Value) + ((1 - wIsC_13_int) * w13.Value)); // If w is C, z is Pole V.

        // =====================================================================================
        // 3. EXTRACT 8 EXTERNAL BOUNDARIES
        // =====================================================================================
        // TOPOLOGICAL MAPPING:
        // The internal faces of the cluster all share the old diagonal (VA, VB) (either meeting
        // at the temporal poles U/V or across the spatial equator). The 8 external boundary faces
        // that touch the rest of the manifold are strictly the faces OPPOSITE VA and VB.
        ref readonly var sD31 = ref mutator.Inspect.GetTetrahedron(idD31);
        ref readonly var sD13 = ref mutator.Inspect.GetTetrahedron(idD13);

        SimplexId ext_C31_oppVA = new(sC31.NeighborSimplices[sC31.FindVertexSlot(context.VA).Value]);
        SimplexId ext_C31_oppVB = new(sC31.NeighborSimplices[sC31.FindVertexSlot(context.VB).Value]);
        var back_C31_oppVA = mutator.Inspect.GetTetrahedron(ext_C31_oppVA).GetFacetFacingSimplexIndex(idC31);
        var back_C31_oppVB = mutator.Inspect.GetTetrahedron(ext_C31_oppVB).GetFacetFacingSimplexIndex(idC31);

        SimplexId ext_D31_oppVA = new(sD31.NeighborSimplices[sD31.FindVertexSlot(context.VA).Value]);
        SimplexId ext_D31_oppVB = new(sD31.NeighborSimplices[sD31.FindVertexSlot(context.VB).Value]);
        var back_D31_oppVA = mutator.Inspect.GetTetrahedron(ext_D31_oppVA).GetFacetFacingSimplexIndex(idD31);
        var back_D31_oppVB = mutator.Inspect.GetTetrahedron(ext_D31_oppVB).GetFacetFacingSimplexIndex(idD31);

        SimplexId ext_C13_oppVA = new(sC13.NeighborSimplices[sC13.FindVertexSlot(context.VA).Value]);
        SimplexId ext_C13_oppVB = new(sC13.NeighborSimplices[sC13.FindVertexSlot(context.VB).Value]);
        var back_C13_oppVA = mutator.Inspect.GetTetrahedron(ext_C13_oppVA).GetFacetFacingSimplexIndex(idC13);
        var back_C13_oppVB = mutator.Inspect.GetTetrahedron(ext_C13_oppVB).GetFacetFacingSimplexIndex(idC13);

        SimplexId ext_D13_oppVA = new(sD13.NeighborSimplices[sD13.FindVertexSlot(context.VA).Value]);
        SimplexId ext_D13_oppVB = new(sD13.NeighborSimplices[sD13.FindVertexSlot(context.VB).Value]);
        var back_D13_oppVA = mutator.Inspect.GetTetrahedron(ext_D13_oppVA).GetFacetFacingSimplexIndex(idD13);
        var back_D13_oppVB = mutator.Inspect.GetTetrahedron(ext_D13_oppVB).GetFacetFacingSimplexIndex(idD13);

        // =====================================================================================
        // 4. TEARDOWN AND CREATION (The Diagonal Flip)
        // =====================================================================================
        // We execute the volume-preserving 4->4 spatial edge flip.
        // The old spacelike diagonal (VA, VB) is destroyed and replaced by (C, D).
        // Each new simplex contains the new spacelike edge (C, D), one of the outer equatorial
        // vertices (VA or VB), and one of the temporal poles (U for (3,1) or V for (1,3)).
        mutator.UnregisterSimplex(idC31); mutator.UnregisterSimplex(idC13);
        mutator.UnregisterSimplex(idD31); mutator.UnregisterSimplex(idD13);

        var nA31 = mutator.RegisterSimplex(context.C, context.D, context.VA, U, SimplexType.ThreeOne);
        var nB31 = mutator.RegisterSimplex(context.C, context.D, context.VB, U, SimplexType.ThreeOne);
        var nA13 = mutator.RegisterSimplex(context.C, context.D, context.VA, V, SimplexType.OneThree);
        var nB13 = mutator.RegisterSimplex(context.C, context.D, context.VB, V, SimplexType.OneThree);

        // =====================================================================================
        // 5. INTERNAL WIRING
        // =====================================================================================
        // Because we registered all 4 new simplices with the exact same prefix layout (C, D, ...),
        // we mathematically guarantee that slot 2 is opposite the axis, and slot 3 is opposite U/V.
        mutator.LinkNeighbors(nA31, new(2), nB31, new(2));
        mutator.LinkNeighbors(nA13, new(2), nB13, new(2));
        mutator.LinkNeighbors(nA31, new(3), nA13, new(3));
        mutator.LinkNeighbors(nB31, new(3), nB13, new(3));

        // =====================================================================================
        // 6. EXTERNAL WIRING
        // =====================================================================================
        // Slot 0 (Opposite C) and Slot 1 (Opposite D) correspond to the external manifold boundaries.
        // We wire the 8 preserved external faces seamlessly into the new cluster.
        mutator.LinkNeighbors(nA31, new(0), ext_D31_oppVB, back_D31_oppVB);
        mutator.LinkNeighbors(nA31, new(1), ext_C31_oppVB, back_C31_oppVB);

        mutator.LinkNeighbors(nB31, new(0), ext_D31_oppVA, back_D31_oppVA);
        mutator.LinkNeighbors(nB31, new(1), ext_C31_oppVA, back_C31_oppVA);

        mutator.LinkNeighbors(nA13, new(0), ext_D13_oppVB, back_D13_oppVB);
        mutator.LinkNeighbors(nA13, new(1), ext_C13_oppVB, back_C13_oppVB);

        mutator.LinkNeighbors(nB13, new(0), ext_D13_oppVA, back_D13_oppVA);
        mutator.LinkNeighbors(nB13, new(1), ext_C13_oppVA, back_C13_oppVA);
    }
}