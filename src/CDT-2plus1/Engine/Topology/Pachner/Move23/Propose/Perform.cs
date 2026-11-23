using CDT_2plus1.Engine.Topology.Pachner.Move23.Propose;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.Topology.TopologyConstants;

namespace CDT_2plus1.Engine.Topology.Pachner.Move23;

internal readonly partial struct Move23
{
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe void PerformMove(MutatingAccessor mutator, in Context context)
    {
        ref readonly var s0 = ref mutator.Inspect.GetTetrahedron(context.Id0);
        ref readonly var s1 = ref mutator.Inspect.GetTetrahedron(context.Id1);

        // =====================================================================================
        // 1. BRANCHLESS EXTRACTION OF SHARED VERTICES (J, K, L)
        // =====================================================================================
        const int bitwiseMask = FacetsPerSimplex - 1; // 3
        var slot = context.FaceSlot0.Value;
        var p = context.FaceSlot0.Value & 1;          // 0 if even, 1 if odd

        var slot_s0_J = (slot + 1) & bitwiseMask;
        var slot_s0_K = (slot + 2 + p) & bitwiseMask;
        var slot_s0_L = (slot + 3 - p) & bitwiseMask;

        VertexId vJ = new(s0.Vertices[slot_s0_J]);
        VertexId vK = new(s0.Vertices[slot_s0_K]);
        VertexId vL = new(s0.Vertices[slot_s0_L]);

        // =====================================================================================
        // 2. EXTRACT 6 EXTERNAL BOUNDARIES
        // =====================================================================================
        // OPTIMIZATION: We already mathematically derived the s0 slots above.
        // We bypass s0.FindVertexSlot() entirely and only search s1.
        var s1_J = s1.FindVertexSlot(vJ);
        var s1_K = s1.FindVertexSlot(vK);
        var s1_L = s1.FindVertexSlot(vL);

        // Get the IDs of the 6 external simplices bound to the outside of this cluster
        SimplexId ext0_oppJ = new(s0.NeighborSimplices[slot_s0_J]);
        SimplexId ext1_oppJ = new(s1.NeighborSimplices[s1_J.Value]);

        SimplexId ext0_oppK = new(s0.NeighborSimplices[slot_s0_K]);
        SimplexId ext1_oppK = new(s1.NeighborSimplices[s1_K.Value]);

        SimplexId ext0_oppL = new(s0.NeighborSimplices[slot_s0_L]);
        SimplexId ext1_oppL = new(s1.NeighborSimplices[s1_L.Value]);

        // Map the returning back-pointers from the external universe so we can wire them
        // to the new simplices later without breaking adjacency.
        var back0_J = mutator.Inspect.GetTetrahedron(ext0_oppJ).GetFacetFacingSimplexIndex(context.Id0);
        var back1_J = mutator.Inspect.GetTetrahedron(ext1_oppJ).GetFacetFacingSimplexIndex(context.Id1);

        var back0_K = mutator.Inspect.GetTetrahedron(ext0_oppK).GetFacetFacingSimplexIndex(context.Id0);
        var back1_K = mutator.Inspect.GetTetrahedron(ext1_oppK).GetFacetFacingSimplexIndex(context.Id1);

        var back0_L = mutator.Inspect.GetTetrahedron(ext0_oppL).GetFacetFacingSimplexIndex(context.Id0);
        var back1_L = mutator.Inspect.GetTetrahedron(ext1_oppL).GetFacetFacingSimplexIndex(context.Id1);

        // =====================================================================================
        // 3. TEARDOWN
        // =====================================================================================
        mutator.UnregisterSimplex(context.Id0);
        mutator.UnregisterSimplex(context.Id1);

        // =====================================================================================
        // 4. CREATION (Strictly Branchless Bitwise Evaluation)
        // =====================================================================================
        // GEOMETRIC CONTEXT:
        // Every new simplex is made of 4 vertices: [Pole1, Pole2, BoundaryA, BoundaryB].
        // Because the pole axis is strictly timelike, EXACTLY ONE pole sits on the lower
        // time slice (minT). To find the simplex type, we just count how many total vertices
        // sit on minT: 1 (from the pole) + (BoundaryA == minT ? 1 : 0) + (BoundaryB == minT ? 1 : 0).
        long tT = mutator.Inspect.GetVertexTime(context.VTarget).Value;
        long tN = mutator.Inspect.GetVertexTime(context.VNeighbour).Value;

        // ---------------------------------------------------------------------------------
        // A. Branchless Min(tT, tN) with Periodic Boundary Wrap-Around
        // ---------------------------------------------------------------------------------
        var diff = tN - tT;

        // diff >> 63 extracts the sign bit and smears it across all 64 bits.
        // If tN < tT, diff is negative, so (diff >> 63) becomes -1 (0xFFFFFFFFFFFFFFFF).
        // If tN >= tT, diff is positive, so (diff >> 63) becomes 0 (0x0000000000000000).
        var minT = tT + (diff & (diff >> 63));
        var maxT = tT + tN - minT;

        // Across the periodic boundary (t = T - 1 wrapping to t = 0), (maxT - minT) > 1,
        // so the lower slice of the slab is maxT (T - 1) rather than minT (0).
        var wrapMask = (1 - (maxT - minT)) >> 63;
        minT ^= (minT ^ maxT) & wrapMask;

        // ---------------------------------------------------------------------------------
        // B. Branchless Equality: Calculate if boundary vertices are on minT
        // ---------------------------------------------------------------------------------
        // Shift the time values down so that if a vertex is exactly on minT, its value becomes 0.
        var tJ = mutator.Inspect.GetVertexTime(vJ).Value - minT;
        var tK = mutator.Inspect.GetVertexTime(vK).Value - minT;
        var tL = mutator.Inspect.GetVertexTime(vL).Value - minT;

        // THE TWO'S COMPLEMENT ZERO TRICK:
        // We want to return 1 if the value is exactly 0, and 0 if it is anything else.
        // In binary two's complement, `-x` flips all bits of `x` and adds 1.
        // This means for ANY non-zero number, either `x` or `-x` will have its highest bit (sign bit) set to 1.
        // Only when x = 0 is the highest bit 0 for both `x` and `-x`.
        // So: (x | -x) forces the highest bit to 1 for everything EXCEPT 0.
        // We bitwise NOT (~) the result to invert it, shifting the 1 to the 0 case.
        // Finally, we shift that highest bit 63 spaces to the right to leave us with a clean int of 1 or 0.
        var bJ = (int)(~((ulong)tJ | (ulong)(-tJ)) >> 63);
        var bK = (int)(~((ulong)tK | (ulong)(-tK)) >> 63);
        var bL = (int)(~((ulong)tL | (ulong)(-tL)) >> 63);

        // ---------------------------------------------------------------------------------
        // C. Branchless Enum Mapping (Shift Register)
        // ---------------------------------------------------------------------------------
        // Now we know exactly how many vertices are on minT.
        // count = 1 (from poles) + b_Boundary1 + b_Boundary2
        // We pack our byte-sized enums into a single 64-bit unsigned integer like an array.
        const ulong PackedTypes =
            ((ulong)SimplexType.OneThree << 8) | // Lives at bit offset 8  (Index 1)
            ((ulong)SimplexType.TwoTwo << 16) |  // Lives at bit offset 16 (Index 2)
            ((ulong)SimplexType.ThreeOne << 24); // Lives at bit offset 24 (Index 3)

        // Calculate the count (e.g., 1 + bJ + bK) and multiply by 8 (using << 3) to get the bit offset.
        // We bitshift the PackedTypes register down by that offset, and mask with 0xFF to grab just that byte.
        var type0 = (SimplexType)((PackedTypes >> ((1 + bJ + bK) << 3)) & 0xFF);
        var type1 = (SimplexType)((PackedTypes >> ((1 + bK + bL) << 3)) & 0xFF);
        var type2 = (SimplexType)((PackedTypes >> ((1 + bL + bJ) << 3)) & 0xFF);

        // ---------------------------------------------------------------------------------
        // D. Registration
        // ---------------------------------------------------------------------------------
        // Register Order MUST BE: (Pole1, Pole2, Boundary1, Boundary2) to satisfy the wiring logic in Section 5.
        var n0 = mutator.RegisterSimplex(context.VTarget, context.VNeighbour, vJ, vK, type0);
        var n1 = mutator.RegisterSimplex(context.VTarget, context.VNeighbour, vK, vL, type1);
        var n2 = mutator.RegisterSimplex(context.VTarget, context.VNeighbour, vL, vJ, type2);

        // =====================================================================================
        // 5. INTERNAL WIRING
        // =====================================================================================
        // Connect the 3 new simplices around the central axis. Because Pole1/Pole2 were forced
        // into Slots 0 and 1 during registration, the shared internal faces are ALWAYS Slots 2 and 3.
        mutator.LinkNeighbors(n0, new(2), n1, new(3)); // Shared face: (VTarget, VNeighbour, vK)
        mutator.LinkNeighbors(n1, new(2), n2, new(3)); // Shared face: (VTarget, VNeighbour, vL)
        mutator.LinkNeighbors(n2, new(2), n0, new(3)); // Shared face: (VTarget, VNeighbour, vJ)

        // =====================================================================================
        // 6. EXTERNAL WIRING
        // =====================================================================================
        // Glue the new cluster back into the manifold.
        // Slot 0 (Opposite VTarget) binds to the boundary that used to belong to S1.
        // Slot 1 (Opposite VNeighbour) binds to the boundary that used to belong to S0.
        mutator.LinkNeighbors(n0, new(0), ext1_oppL, back1_L);
        mutator.LinkNeighbors(n0, new(1), ext0_oppL, back0_L);

        mutator.LinkNeighbors(n1, new(0), ext1_oppJ, back1_J);
        mutator.LinkNeighbors(n1, new(1), ext0_oppJ, back0_J);

        mutator.LinkNeighbors(n2, new(0), ext1_oppK, back1_K);
        mutator.LinkNeighbors(n2, new(1), ext0_oppK, back0_K);
    }
}