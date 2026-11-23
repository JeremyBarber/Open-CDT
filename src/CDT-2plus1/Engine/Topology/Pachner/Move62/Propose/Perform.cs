using CDT_2plus1.Engine.Topology.Pachner.Move62.Propose;
using CDT_Agnostic.RuntimeMemory.Identifiers;
using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1.Engine.Topology.Pachner.Move62;

internal readonly partial struct Move62
{
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe void PerformMove(MutatingAccessor mutator, in Context context)
    {
        ref readonly var s31_0 = ref mutator.Inspect.GetTetrahedron(context.Id31_0);
        ref readonly var s31_1 = ref mutator.Inspect.GetTetrahedron(context.Id31_1);
        ref readonly var s31_2 = ref mutator.Inspect.GetTetrahedron(context.Id31_2);

        ref readonly var s13_0 = ref mutator.Inspect.GetTetrahedron(context.Id13_0);
        ref readonly var s13_1 = ref mutator.Inspect.GetTetrahedron(context.Id13_1);
        ref readonly var s13_2 = ref mutator.Inspect.GetTetrahedron(context.Id13_2);

        // =====================================================================================
        // THE SUM TRICK: BRANCHLESS MATHEMATICAL BOUNDARY CLASSIFICATION
        // =====================================================================================
        // ALGEBRAIC PROOF:
        // An old (3,1) simplex in this cluster contains {vTarget, U, X, Y} where X,Y are in {A,B,C}.
        // The total sum of the spatial equator is eqSum = A + B + C.
        // The sum of the timelike pole axis is p31 = vTarget + U.
        // The missing equatorial vertex (Z) can be found algebraically: Z = eqSum - (X + Y).
        // Therefore, Z = eqSum - (TotalSimplexSum - p31).
        // This isolates the exact missing spatial vertex in O(1) time without looping or arrays.
        var eqSum = context.A.Value + context.B.Value + context.C.Value;

        var p31 = context.VTarget.Value + context.U.Value;
        var p13 = context.VTarget.Value + context.V.Value;

        // Extract the missing spatial vertex ID for each of the 6 simplices
        // Using .Value explicitly to ensure clean integer arithmetic
        var lack31_0 = eqSum - (s31_0.Vertices[0] + s31_0.Vertices[1] + s31_0.Vertices[2] + s31_0.Vertices[3] - p31);
        var lack31_1 = eqSum - (s31_1.Vertices[0] + s31_1.Vertices[1] + s31_1.Vertices[2] + s31_1.Vertices[3] - p31);
        var lack31_2 = eqSum - (s31_2.Vertices[0] + s31_2.Vertices[1] + s31_2.Vertices[2] + s31_2.Vertices[3] - p31);

        var lack13_0 = eqSum - (s13_0.Vertices[0] + s13_0.Vertices[1] + s13_0.Vertices[2] + s13_0.Vertices[3] - p13);
        var lack13_1 = eqSum - (s13_1.Vertices[0] + s13_1.Vertices[1] + s13_1.Vertices[2] + s13_1.Vertices[3] - p13);
        var lack13_2 = eqSum - (s13_2.Vertices[0] + s13_2.Vertices[1] + s13_2.Vertices[2] + s13_2.Vertices[3] - p13);

        // Branchless Multiplexer: Route the simplex ID based on which vertex it is missing.
        SimplexId id31_oppA = new((lack31_0 == context.A.Value ? 1 : 0) * context.Id31_0.Value + (lack31_1 == context.A.Value ? 1 : 0) * context.Id31_1.Value + (lack31_2 == context.A.Value ? 1 : 0) * context.Id31_2.Value);
        SimplexId id31_oppB = new((lack31_0 == context.B.Value ? 1 : 0) * context.Id31_0.Value + (lack31_1 == context.B.Value ? 1 : 0) * context.Id31_1.Value + (lack31_2 == context.B.Value ? 1 : 0) * context.Id31_2.Value);
        SimplexId id31_oppC = new((lack31_0 == context.C.Value ? 1 : 0) * context.Id31_0.Value + (lack31_1 == context.C.Value ? 1 : 0) * context.Id31_1.Value + (lack31_2 == context.C.Value ? 1 : 0) * context.Id31_2.Value);

        SimplexId id13_oppA = new((lack13_0 == context.A.Value ? 1 : 0) * context.Id13_0.Value + (lack13_1 == context.A.Value ? 1 : 0) * context.Id13_1.Value + (lack13_2 == context.A.Value ? 1 : 0) * context.Id13_2.Value);
        SimplexId id13_oppB = new((lack13_0 == context.B.Value ? 1 : 0) * context.Id13_0.Value + (lack13_1 == context.B.Value ? 1 : 0) * context.Id13_1.Value + (lack13_2 == context.B.Value ? 1 : 0) * context.Id13_2.Value);
        SimplexId id13_oppC = new((lack13_0 == context.C.Value ? 1 : 0) * context.Id13_0.Value + (lack13_1 == context.C.Value ? 1 : 0) * context.Id13_1.Value + (lack13_2 == context.C.Value ? 1 : 0) * context.Id13_2.Value);

        // =====================================================================================
        // 1. EXTRACT 6 EXTERNAL BOUNDARIES
        // =====================================================================================
        // TOPOLOGICAL MAPPING:
        // Why does the old simplex missing 'A' hold the external boundary for our new 'oppA' face?
        // The old simplex missing 'A' contains {vTarget, U, B, C}. 
        // Its external face opposite vTarget is exactly {U, B, C}.
        // In our newly created simplex {A, B, C, U}, the face opposite 'A' is also exactly {U, B, C}.
        // Thus, the old simplex missing 'A' perfectly maps to the new 'oppA' boundary.
        ref readonly var t31_oppA = ref mutator.Inspect.GetTetrahedron(id31_oppA);
        ref readonly var t31_oppB = ref mutator.Inspect.GetTetrahedron(id31_oppB);
        ref readonly var t31_oppC = ref mutator.Inspect.GetTetrahedron(id31_oppC);

        ref readonly var t13_oppA = ref mutator.Inspect.GetTetrahedron(id13_oppA);
        ref readonly var t13_oppB = ref mutator.Inspect.GetTetrahedron(id13_oppB);
        ref readonly var t13_oppC = ref mutator.Inspect.GetTetrahedron(id13_oppC);

        SimplexId ext_31_oppA = new(t31_oppA.NeighborSimplices[t31_oppA.FindVertexSlot(context.VTarget).Value]);
        SimplexId ext_31_oppB = new(t31_oppB.NeighborSimplices[t31_oppB.FindVertexSlot(context.VTarget).Value]);
        SimplexId ext_31_oppC = new(t31_oppC.NeighborSimplices[t31_oppC.FindVertexSlot(context.VTarget).Value]);

        var back_31_oppA = mutator.Inspect.GetTetrahedron(ext_31_oppA).GetFacetFacingSimplexIndex(id31_oppA);
        var back_31_oppB = mutator.Inspect.GetTetrahedron(ext_31_oppB).GetFacetFacingSimplexIndex(id31_oppB);
        var back_31_oppC = mutator.Inspect.GetTetrahedron(ext_31_oppC).GetFacetFacingSimplexIndex(id31_oppC);

        SimplexId ext_13_oppA = new(t13_oppA.NeighborSimplices[t13_oppA.FindVertexSlot(context.VTarget).Value]);
        SimplexId ext_13_oppB = new(t13_oppB.NeighborSimplices[t13_oppB.FindVertexSlot(context.VTarget).Value]);
        SimplexId ext_13_oppC = new(t13_oppC.NeighborSimplices[t13_oppC.FindVertexSlot(context.VTarget).Value]);

        var back_13_oppA = mutator.Inspect.GetTetrahedron(ext_13_oppA).GetFacetFacingSimplexIndex(id13_oppA);
        var back_13_oppB = mutator.Inspect.GetTetrahedron(ext_13_oppB).GetFacetFacingSimplexIndex(id13_oppB);
        var back_13_oppC = mutator.Inspect.GetTetrahedron(ext_13_oppC).GetFacetFacingSimplexIndex(id13_oppC);

        // =====================================================================================
        // 2. TEARDOWN
        // =====================================================================================
        mutator.UnregisterSimplex(id31_oppA); mutator.UnregisterSimplex(id31_oppB); mutator.UnregisterSimplex(id31_oppC);
        mutator.UnregisterSimplex(id13_oppA); mutator.UnregisterSimplex(id13_oppB); mutator.UnregisterSimplex(id13_oppC);

        mutator.RemoveVertex(context.VTarget);

        // =====================================================================================
        // 3. CREATION
        // =====================================================================================
        // Creates the new foliated bipyramid: one upward (3,1) and one downward (1,3)
        // cleanly eliminating the central vTarget.
        var n31 = mutator.RegisterSimplex(context.A, context.B, context.C, context.U, SimplexType.ThreeOne);
        var n13 = mutator.RegisterSimplex(context.A, context.B, context.C, context.V, SimplexType.OneThree);

        // =====================================================================================
        // 4. DYNAMIC WIRING (AGNOSTIC TO VERTEX SORTING)
        // =====================================================================================
        ref readonly var r_n31 = ref mutator.Inspect.GetTetrahedron(n31);
        ref readonly var r_n13 = ref mutator.Inspect.GetTetrahedron(n13);

        // Find exactly where the memory allocator/sorter placed the poles...
        LocalFacetSlot n31_slotU = new(r_n31.FindVertexSlot(context.U).Value);
        LocalFacetSlot n13_slotV = new(r_n13.FindVertexSlot(context.V).Value);

        // ...and exactly where it placed the equator vertices.
        LocalFacetSlot n31_slotA = new(r_n31.FindVertexSlot(context.A).Value);
        LocalFacetSlot n31_slotB = new(r_n31.FindVertexSlot(context.B).Value);
        LocalFacetSlot n31_slotC = new(r_n31.FindVertexSlot(context.C).Value);

        LocalFacetSlot n13_slotA = new(r_n13.FindVertexSlot(context.A).Value);
        LocalFacetSlot n13_slotB = new(r_n13.FindVertexSlot(context.B).Value);
        LocalFacetSlot n13_slotC = new(r_n13.FindVertexSlot(context.C).Value);

        // Because the internal CDT geometric convention strictly enforces that 
        // Facet 'i' is opposite Vertex 'i'
        mutator.LinkNeighbors(n31, n31_slotU, n13, n13_slotV); // Glues the spatial equator (A,B,C)

        // Wire the 6 outer boundaries seamlessly
        mutator.LinkNeighbors(n31, n31_slotA, ext_31_oppA, back_31_oppA);
        mutator.LinkNeighbors(n13, n13_slotA, ext_13_oppA, back_13_oppA);

        mutator.LinkNeighbors(n31, n31_slotB, ext_31_oppB, back_31_oppB);
        mutator.LinkNeighbors(n13, n13_slotB, ext_13_oppB, back_13_oppB);

        mutator.LinkNeighbors(n31, n31_slotC, ext_31_oppC, back_31_oppC);
        mutator.LinkNeighbors(n13, n13_slotC, ext_13_oppC, back_13_oppC);
    }
}