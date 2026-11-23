using CDT_2plus1.Engine.Topology.Pachner.Move26.Propose;
using CDT_Agnostic.RuntimeMemory.Identifiers;
using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.Topology.TopologyConstants;

namespace CDT_2plus1.Engine.Topology.Pachner.Move26;

/// <summary>
/// Executes the (2,6) Pachner Move for 2+1D Causal Dynamical Triangulations.
/// Performs a spatial expansion by inserting a new vertex into the center of a 
/// spacelike equator shared by a (3,1) and a (1,3) simplex, splitting them into 6 new simplices.
/// </summary>
internal readonly partial struct Move26
{
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe void PerformMove(MutatingAccessor mutator, in Context context)
    {
        ref readonly var s13 = ref mutator.Inspect.GetTetrahedron(context.Id13);
        ref readonly var s31 = ref mutator.Inspect.GetTetrahedron(context.Id31);

        // =====================================================================================
        // 1. BRANCHLESS POLE EXTRACTION (Agnostic to Sorted Arrays)
        // =====================================================================================
        // By definition, a (3,1) simplex has 3 vertices on the spatial equator and 1 Top Pole.
        // We evaluate equality against the known equator time. Exactly one vertex will differ.
        // Multiplying the boolean result (0 or 1) by the vertex ID mathematically isolates the Pole.
        VertexId u0 = new(s31.Vertices[0]); VertexId u1 = new(s31.Vertices[1]);
        VertexId u2 = new(s31.Vertices[2]); VertexId u3 = new(s31.Vertices[3]);

        var isU0 = mutator.Inspect.GetVertexTime(u0) != context.TimeEquator ? (byte)1 : (byte)0;
        var isU1 = mutator.Inspect.GetVertexTime(u1) != context.TimeEquator ? (byte)1 : (byte)0;
        var isU2 = mutator.Inspect.GetVertexTime(u2) != context.TimeEquator ? (byte)1 : (byte)0;
        var isU3 = mutator.Inspect.GetVertexTime(u3) != context.TimeEquator ? (byte)1 : (byte)0;

        VertexId vUp = new((isU0 * u0.Value) + (isU1 * u1.Value) + (isU2 * u2.Value) + (isU3 * u3.Value));

        VertexId d0 = new(s13.Vertices[0]); VertexId d1 = new(s13.Vertices[1]);
        VertexId d2 = new(s13.Vertices[2]); VertexId d3 = new(s13.Vertices[3]);

        var isD0 = mutator.Inspect.GetVertexTime(d0) != context.TimeEquator ? (byte)1 : (byte)0;
        var isD1 = mutator.Inspect.GetVertexTime(d1) != context.TimeEquator ? (byte)1 : (byte)0;
        var isD2 = mutator.Inspect.GetVertexTime(d2) != context.TimeEquator ? (byte)1 : (byte)0;
        var isD3 = mutator.Inspect.GetVertexTime(d3) != context.TimeEquator ? (byte)1 : (byte)0;

        VertexId vDown = new((isD0 * d0.Value) + (isD1 * d1.Value) + (isD2 * d2.Value) + (isD3 * d3.Value));

        // =====================================================================================
        // 1B. EXTRACT EQUATORIAL TRIANGLE (A, B, C)
        // =====================================================================================
        // Now that we perfectly know the pole (vUp), the remaining 3 vertices in S31 MUST 
        // form the spacelike equator. 
        // Note: The JIT compiler completely unrolls this fixed 4-step loop.
        VertexId a = default, b = default, c = default;
        var eqCount = 0;

        for (var i = 0; i < FacetsPerSimplex; i++)
        {
            VertexId v = new(s31.Vertices[i]);
            if (v == vUp) continue;

            if (eqCount == 0) a = v;
            else if (eqCount == 1) b = v;
            else c = v;

            eqCount++;
        }

        // =====================================================================================
        // 2. EXTRACT 6 EXTERNAL BOUNDARIES (S31 & S13)
        // =====================================================================================
        var s31_A = s31.FindVertexSlot(a); var s13_A = s13.FindVertexSlot(a);
        var s31_B = s31.FindVertexSlot(b); var s13_B = s13.FindVertexSlot(b);
        var s31_C = s31.FindVertexSlot(c); var s13_C = s13.FindVertexSlot(c);

        SimplexId ext_31_oppA = new(s31.NeighborSimplices[s31_A.Value]); SimplexId ext_13_oppA = new(s13.NeighborSimplices[s13_A.Value]);
        SimplexId ext_31_oppB = new(s31.NeighborSimplices[s31_B.Value]); SimplexId ext_13_oppB = new(s13.NeighborSimplices[s13_B.Value]);
        SimplexId ext_31_oppC = new(s31.NeighborSimplices[s31_C.Value]); SimplexId ext_13_oppC = new(s13.NeighborSimplices[s13_C.Value]);

        var back_31_oppA = mutator.Inspect.GetTetrahedron(ext_31_oppA).GetFacetFacingSimplexIndex(context.Id31);
        var back_31_oppB = mutator.Inspect.GetTetrahedron(ext_31_oppB).GetFacetFacingSimplexIndex(context.Id31);
        var back_31_oppC = mutator.Inspect.GetTetrahedron(ext_31_oppC).GetFacetFacingSimplexIndex(context.Id31);

        var back_13_oppA = mutator.Inspect.GetTetrahedron(ext_13_oppA).GetFacetFacingSimplexIndex(context.Id13);
        var back_13_oppB = mutator.Inspect.GetTetrahedron(ext_13_oppB).GetFacetFacingSimplexIndex(context.Id13);
        var back_13_oppC = mutator.Inspect.GetTetrahedron(ext_13_oppC).GetFacetFacingSimplexIndex(context.Id13);

        // =====================================================================================
        // 3. TEARDOWN AND VERTEX CREATION
        // =====================================================================================
        mutator.UnregisterSimplex(context.Id31);
        mutator.UnregisterSimplex(context.Id13);

        var vNew = mutator.AddVertex(context.TimeEquator);

        // =====================================================================================
        // 4. CREATION (Strict Slot Mapping)
        // =====================================================================================
        // The RegisterSimplex signature is (v0, v1, v2, v3).
        // By rigidly ordering the inputs as (EquatorX, EquatorY, vNew, Pole), we guarantee:
        // - Slot 0 (Opposite X): Internal horizontal boundary sharing (EquatorY, vNew, Pole)
        // - Slot 1 (Opposite Y): Internal horizontal boundary sharing (EquatorX, vNew, Pole)
        // - Slot 2 (Opposite vNew): External boundary sharing (EquatorX, EquatorY, Pole)
        // - Slot 3 (Opposite Pole): Internal vertical boundary (the new spacelike equator)
        var n31_A = mutator.RegisterSimplex(b, c, vNew, vUp, SimplexType.ThreeOne);
        var n31_B = mutator.RegisterSimplex(c, a, vNew, vUp, SimplexType.ThreeOne);
        var n31_C = mutator.RegisterSimplex(a, b, vNew, vUp, SimplexType.ThreeOne);

        var n13_A = mutator.RegisterSimplex(b, c, vNew, vDown, SimplexType.OneThree);
        var n13_B = mutator.RegisterSimplex(c, a, vNew, vDown, SimplexType.OneThree);
        var n13_C = mutator.RegisterSimplex(a, b, vNew, vDown, SimplexType.OneThree);

        // =====================================================================================
        // 5. INTERNAL VERTICAL WIRING (Opposite Pole at Slot 3)
        // =====================================================================================
        // Glues the 3 newly sliced upper tetrahedra to the 3 lower tetrahedra along the equator.
        mutator.LinkNeighbors(n31_A, new(3), n13_A, new(3));
        mutator.LinkNeighbors(n31_B, new(3), n13_B, new(3));
        mutator.LinkNeighbors(n31_C, new(3), n13_C, new(3));

        // =====================================================================================
        // 6. INTERNAL HORIZONTAL WIRING (Radiating out from vNew)
        // =====================================================================================
        // A0 (c, vNew, Pole) links to B1 (c, vNew, Pole)
        mutator.LinkNeighbors(n31_A, new(0), n31_B, new(1));
        mutator.LinkNeighbors(n13_A, new(0), n13_B, new(1));

        // B0 (a, vNew, Pole) links to C1 (a, vNew, Pole)
        mutator.LinkNeighbors(n31_B, new(0), n31_C, new(1));
        mutator.LinkNeighbors(n13_B, new(0), n13_C, new(1));

        // C0 (b, vNew, Pole) links to A1 (b, vNew, Pole)
        mutator.LinkNeighbors(n31_C, new(0), n31_A, new(1));
        mutator.LinkNeighbors(n13_C, new(0), n13_A, new(1));

        // =====================================================================================
        // 7. EXTERNAL WIRING (Opposite vNew at Slot 2)
        // =====================================================================================
        // Glues the 6 outer triangular faces of our new cluster back into the bulk manifold.
        mutator.LinkNeighbors(n31_A, new(2), ext_31_oppA, back_31_oppA);
        mutator.LinkNeighbors(n13_A, new(2), ext_13_oppA, back_13_oppA);

        mutator.LinkNeighbors(n31_B, new(2), ext_31_oppB, back_31_oppB);
        mutator.LinkNeighbors(n13_B, new(2), ext_13_oppB, back_13_oppB);

        mutator.LinkNeighbors(n31_C, new(2), ext_31_oppC, back_31_oppC);
        mutator.LinkNeighbors(n13_C, new(2), ext_13_oppC, back_13_oppC);
    }
}